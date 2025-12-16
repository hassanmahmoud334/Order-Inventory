using Grpc.Core;
using Microsoft.Extensions.Logging;
using Order_Service.Data;
using Order_Service.Messaging;
using Order_Service.Models;
using System.Text.Json;

namespace Order_Service.Services
{
    public class OrderService : PlaceOrder.PlaceOrderBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<OrderService> _logger;

        public OrderService(ILogger<OrderService> logger, AppDbContext db)
        {
            _db = db;
            _logger = logger;   
        }
        public override async Task<PlaceOrderResponse> PlaceOrder(
            PlaceOrderRequest request,
            ServerCallContext context)
        {
            _logger.LogInformation(
                "Processing order for ProductId: {ProductId}, Quantity: {Quantity}",
                request.ProductId,
                request.Quantity);

            var orderId = Guid.NewGuid();

            var order = new Order
            {
                Id = orderId,
                Sku = Convert.ToString(request.ProductId),
                Quantity = request.Quantity
            };

            var orderEvent = new
            {
                OrderId = orderId,
                ProductId = request.ProductId,
                Quantity = request.Quantity
            };

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "OrderCreated",
                Payload = JsonSerializer.Serialize(orderEvent),
                CreatedAt = DateTime.UtcNow
            };


            await using var tx = await _db.Database.BeginTransactionAsync();

            _db.Orders.Add(order);
            _db.OutboxMessages.Add(outboxMessage);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return new PlaceOrderResponse
            {
                OrderId = orderId.ToString(),
                Success = true,
                Message = "Your order has been placed."
            };
        }
    }
}
