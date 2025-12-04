using Grpc.Core;
using Microsoft.Extensions.Logging;
using Order_Service.Messaging;

namespace Order_Service.Services
{
    public class OrderService : PlaceOrder.PlaceOrderBase
    {
        private readonly IMessageProducer _messageProducer;
        private readonly ILogger _logger;

        public OrderService(ILogger<OrderService> logger, IMessageProducer messageProducer)
        {
            _messageProducer = messageProducer;
            _logger = logger;   
        }
        public override async Task<PlaceOrderResponse> PlaceOrder(PlaceOrderRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Processing order for ProductId: {ProductId}, Quantity: {Quantity}", request.ProductId, request.Quantity);

            var orderEvent = new
            {
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                OrderId = Guid.NewGuid().ToString()
            };
            
             _messageProducer.SendMessageAsync(orderEvent);
            var response = new PlaceOrderResponse
            {
                OrderId = orderEvent.OrderId,
                Success = true,
                Message = "Your order has been placed."
            };

            return response;
        }
    }
}
