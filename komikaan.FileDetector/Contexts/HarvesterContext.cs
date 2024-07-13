using System.Text;
using RabbitMQ.Client;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace komikaan.FileDetector.Contexts
{
    public class HarvesterContext
    {
        private IModel _channel;
        private readonly IConfiguration _configuration;

        public HarvesterContext(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task StartAsync(CancellationToken token)
        {

            var factory = new ConnectionFactory();

            factory.HostName = _configuration.GetValue<string>("RabbitMQHost")!;
            factory.UserName = _configuration.GetValue<string>("RabbitMQUsername")!;
            factory.Password = _configuration.GetValue<string>("RabbitMQPassword")!;

            var connection = factory.CreateConnection();
            _channel = connection.CreateModel();

            _channel.ExchangeDeclare("harvester-notifications", ExchangeType.Direct, durable: true);

            return Task.CompletedTask;
        }

        public Task SendMessageAsync(object message)
        {
            var options = new JsonSerializerOptions
            {
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
            };

            var rawMessage = JsonSerializer.Serialize(message, options);
            var body = Encoding.UTF8.GetBytes(rawMessage);
            _channel.BasicPublish(exchange: "harvester-notifications",
                                 routingKey: "harvester",
                                 body: body);
            return Task.CompletedTask;
        }
    }
}
