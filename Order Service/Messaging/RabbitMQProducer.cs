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

                //await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Direct);
                await channel.QueueDeclareAsync(_queueName, true, false, false, null);
                //await channel.QueueBindAsync(_queueName, exchangeName, routingKey, null);


                var messageBody = Encoding.UTF8.GetBytes(payload);
                var props = new BasicProperties();
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
