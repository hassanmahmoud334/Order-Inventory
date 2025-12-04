using RabbitMQ.Client;
using System.Text.Json.Serialization;

namespace Order_Service.Messaging
{
    public class RabbitMQProducer : IMessageProducer
    {
        public async void SendMessageAsync<T>(T message)
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            IConnection connection = await factory.CreateConnectionAsync();
            IChannel channel = await connection.CreateChannelAsync();

            string exchangeName = "orderExchange";
            string routingKey = "orderRoutingKey";
            string queueName = "orderQueue";

            await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Direct);
            await channel.QueueDeclareAsync(queueName, false, false, false, null);
            await channel.QueueBindAsync(queueName, exchangeName, routingKey, null);


            var messageBody = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(message, new System.Text.Json.JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            var props = new BasicProperties();
            await channel.BasicPublishAsync(exchangeName, routingKey,false , props, messageBody);


            await channel.CloseAsync();
            await connection.CloseAsync();

        }
    }
}
