using RabbitMQ.Client;

namespace Order_Service.Messaging
{
    public static class RabbitMQBootstrap
    {
        public static async Task InitializeAsync(IChannel channel)
        {
            // Exchanges
            await channel.ExchangeDeclareAsync(
                exchange: "order.dlx",
                type: ExchangeType.Direct,
                durable: true
            );

            // Main Queue → retry
            var mainQueueArgs = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", "order.dlx" },
                { "x-dead-letter-routing-key", "order.dead" }
            };

            await channel.QueueDeclareAsync(
                queue: "orderQueue",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: mainQueueArgs
            );

            // Retry Queue (TTL → back to main)
            var retryQueueArgs = new Dictionary<string, object>
            {
                { "x-message-ttl", 5000 }, // 5 seconds
                { "x-dead-letter-exchange", "" },
                { "x-dead-letter-routing-key", "orderQueue" }
            };

            await channel.QueueDeclareAsync(
                queue: "orderQueue.retry",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: retryQueueArgs
            );

            // DLQ
            await channel.QueueDeclareAsync(
                queue: "orderQueue.dlq",
                durable: true,
                exclusive: false,
                autoDelete: false
            );

            await channel.QueueBindAsync(
                queue: "orderQueue.dlq",
                exchange: "order.dlx",
                routingKey: "order.dead"
            );
        }
    }

}
