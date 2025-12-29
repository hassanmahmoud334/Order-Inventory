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
        public async Task PublishAsync(string queueName, string payload)
        {

            var factory = new ConnectionFactory 
            {   HostName = "localhost",
                UserName = "guest",
                Password = "guest"
            };
            IConnection connection = await factory.CreateConnectionAsync();
            IChannel channel = await connection.CreateChannelAsync();

            try
            {

                string exchangeName = "";
                string routingKey = queueName;
                string _queueName = queueName;

                // Declare DLX
                await channel.ExchangeDeclareAsync(
                    exchange: "order.dlx",
                    type: ExchangeType.Direct,
                    durable: true
                );

                // Declare DLQ
                await channel.QueueDeclareAsync(
                    queue: "orderQueue.dlq",
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                );

                await channel.QueueBindAsync(
                    queue: "orderQueue.dlq",
                    exchange: "order.dlx",
                    routingKey: "order.dead"
                );

                // Declare main queue WITH DLQ settings
                var args = new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", "order.dlx" },
                    { "x-dead-letter-routing-key", "order.dead" }
                };

                //await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Direct);
                await channel.QueueDeclareAsync(_queueName, true, false, false, args);
                //await channel.QueueBindAsync(_queueName, exchangeName, routingKey, null);


                var messageBody = Encoding.UTF8.GetBytes(payload);
                var props = new BasicProperties { Persistent = true };
                await channel.BasicPublishAsync(exchangeName, routingKey, false, props, messageBody);


                await channel.CloseAsync();
                await connection.CloseAsync();


                return;

            }
            catch (Exception ex)
            {
                await channel.CloseAsync();
                await connection.CloseAsync();
                Console.WriteLine($"Error publishing message: {ex.Message}");
                throw;
            }
            

        }
    }
}
