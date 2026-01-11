using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http.HttpResults;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Order_Service.Messaging
{
    public class RabbitMQProducer : IMessageProducer
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        public RabbitMQProducer()
        {
            var factory = new ConnectionFactory
                {
                    HostName = "localhost",
                    UserName = "guest",
                    Password = "guest"
                };
            _connection =  factory.CreateConnectionAsync().Result;
            _channel =  _connection.CreateChannelAsync().Result;

            // 🔥 Declare infra ONCE
            RabbitMQBootstrap.InitializeAsync(_channel).Wait();
        }
        public async Task PublishAsync(string queueName, string payload)
        {
            try
            {
                var messageBody = Encoding.UTF8.GetBytes(payload);
                var props = new BasicProperties
                {
                    Persistent = true,
                    Headers = new Dictionary<string, object>
                    {
                        { "x-retry-count", 0 }
                    }
                };
                await _channel.BasicPublishAsync("", queueName, false, props, messageBody);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error publishing message: {ex.Message}");
            }
        }
        public void Dispose()
        {
            _channel?.CloseAsync().Wait();
            _connection?.CloseAsync().Wait();
        }
    }
}
