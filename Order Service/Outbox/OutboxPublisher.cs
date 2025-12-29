using Microsoft.EntityFrameworkCore;
using Order_Service.Data;
using Order_Service.Messaging;
using System.Text.Json;

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
                        var envelope = new
                        {
                            EventId = msg.Id,
                            Type = msg.Type,
                            ProductId = JsonDocument.Parse(msg.Payload).RootElement.GetProperty("ProductId").GetInt32(),
                            Quantity = JsonDocument.Parse(msg.Payload).RootElement.GetProperty("Quantity").GetInt32(),
                            OccurredAt = msg.CreatedAt
                        };

                        var json = JsonSerializer.Serialize(envelope);

                        await _publisher.PublishAsync("orderQueue", json);

                        msg.ProcessedAt = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Outbox publish failed: {ex.Message}");
                    }
                }

                await db.SaveChangesAsync(stoppingToken);
                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}
