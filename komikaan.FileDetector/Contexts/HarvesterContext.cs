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

                await _channel.ExchangeDeclareAsync("harvester-notifications", ExchangeType.Direct, durable: true);

                await _channel.QueueDeclareAsync(
                    queue: "harvesters",
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: new Dictionary<string, object>
                    {
                        { "x-queue-type", "quorum" }
                    }); 

                await _channel.QueueBindAsync("harvesters", "harvester-notifications", "harvester");

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
            await _channel.BasicPublishAsync(exchange: "harvester-notifications",
                                 routingKey: "harvester",
                                 body: body);
        }
    }
}
