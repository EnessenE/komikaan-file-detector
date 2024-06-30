using System.Text;
using RabbitMQ.Client;
using System.Text.Json;

namespace komikaan.FileDetector.Contexts
{
    public class HarvesterContext
    {
        private IModel _channel;

        public Task StartAsync(CancellationToken token)
        {

            var factory = new ConnectionFactory { HostName = "localhost" };
            var connection = factory.CreateConnection();
            _channel = connection.CreateModel();


            _channel.ExchangeDeclare("harvester-notifications", "direct", durable: true);
            _channel.QueueDeclare(queue: "harvesters",
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

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
            _channel.BasicPublish(exchange: string.Empty,
                                 routingKey: "harvester",
                                 basicProperties: null,
                                 body: body);
            return Task.CompletedTask;
        }
    }
}
