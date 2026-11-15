using System.Text;
using RabbitMQ.Client;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace komikaan.FileDetector.Contexts
{
    public class HarvesterContext
    {
        private IChannel _channel;
        private IConnection _connection;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HarvesterContext> _logger;

        public HarvesterContext(IConfiguration configuration, ILogger<HarvesterContext> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken token)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _configuration.GetValue<string>("RabbitMQHost")!,
                    UserName = _configuration.GetValue<string>("RabbitMQUsername")!,
                    Password = _configuration.GetValue<string>("RabbitMQPassword")!
                };

                _connection = await factory.CreateConnectionAsync();

                _connection.ConnectionShutdownAsync += (sender, ea) =>
                {
                    _logger.LogWarning("RabbitMQ connection shutdown: {Reason}", ea.ReplyText);
                    return Task.CompletedTask;
                };

                _connection.CallbackExceptionAsync += (sender, ea) =>
                {
                    _logger.LogError(ea.Exception, "RabbitMQ connection callback exception occurred");
                    return Task.CompletedTask;
                };

                _connection.ConnectionBlockedAsync += (sender, ea) =>
                {
                    _logger.LogError("RabbitMQ ConnectionBlockedAsync exception occurred: {reason}", ea.Reason);
                    return Task.CompletedTask;
                };

                _channel = await _connection.CreateChannelAsync();

                await _channel.ExchangeDeclarePassiveAsync("harvester-notifications");
                await _channel.QueueDeclarePassiveAsync(queue: "harvesters");

                RabbitMQ.Client.Events.AsyncEventHandler<RabbitMQ.Client.Events.ShutdownEventArgs> asyncEventHandler = (sender, ea) =>
                {
                    _logger.LogWarning("RabbitMQ channel shutdown: {Reason}", ea.ReplyText);
                    return Task.CompletedTask;
                };
                _channel.ChannelShutdownAsync += asyncEventHandler;

                _channel.CallbackExceptionAsync += (sender, ea) =>
                {
                    _logger.LogError(ea.Exception, "RabbitMQ channel callback exception occurred");
                    return Task.CompletedTask;
                };

                _logger.LogInformation("RabbitMQ connection and channel established successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start RabbitMQ channel.");
                throw;
            }

        }

        private Task _connection_ConnectionBlockedAsync(object sender, RabbitMQ.Client.Events.ConnectionBlockedEventArgs @event)
        {
            throw new NotImplementedException();
        }

        public async Task SendMessageAsync(object message)
        {
            var options = new JsonSerializerOptions
            {
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
            };

            var rawMessage = JsonSerializer.Serialize(message, options);
            var body = Encoding.UTF8.GetBytes(rawMessage);

            var properties = new BasicProperties { Persistent = true };
            if (message is DatabaseSupplierConfiguration { DelayImportBy: { } delay } supplier
                && delay > TimeSpan.Zero)
            {
                // RabbitMQ 4.4+ holds the message in the (quorum) queue until this time,
                // instead of the consumer delaying and requeueing it in a loop.
                var deliveryTime = supplier.ImportRequestedAt + delay;
                if (deliveryTime > DateTimeOffset.UtcNow)
                {
                    properties.Headers = new Dictionary<string, object?>
                    {
                        ["x-opt-delivery-time"] = deliveryTime.ToUnixTimeMilliseconds()
                    };
                    _logger.LogInformation("Delaying import request for {name} until {time:R}", supplier.Name, deliveryTime);
                }
            }

            await _channel.BasicPublishAsync(exchange: "harvester-notifications",
                                 routingKey: "harvester",
                                 mandatory: false,
                                 basicProperties: properties,
                                 body: body);
        }
    }
}
