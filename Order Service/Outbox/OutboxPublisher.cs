using Microsoft.EntityFrameworkCore;
using Order_Service.Data;
using Order_Service.Messaging;

namespace Order_Service.Outbox
{
    public class OutboxPublisher : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMessageProducer _publisher;

        public OutboxPublisher(
            IServiceScopeFactory scopeFactory,
            IMessageProducer publisher)
        {
            _scopeFactory = scopeFactory;
            _publisher = publisher;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var messages = await db.OutboxMessages
                    .Where(x => x.ProcessedAt == null)
                    .OrderBy(x => x.CreatedAt)
                    .Take(10)
                    .ToListAsync(stoppingToken);

                foreach (var msg in messages)
                {
                    try
                    {
                        await _publisher.PublishAsync("orderQueue", msg.Payload);
                        msg.ProcessedAt = DateTime.UtcNow;
                    }
                    catch
                    {
                        // RabbitMQ down → retry later
                    }
                }

                await db.SaveChangesAsync(stoppingToken);
                await Task.Delay(2000, stoppingToken);
            }
        }
    }

}
