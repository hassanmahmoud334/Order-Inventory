using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Order_Service.Services
{
    public class OrderService(ILogger<OrderService> logger) : PlaceOrder.PlaceOrderBase
    {
        public override Task<PlaceOrderResponse> PlaceOrder(PlaceOrderRequest request, ServerCallContext context)
        {
            logger.LogInformation("Processing order for ProductId: {ProductId}, Quantity: {Quantity}", request.ProductId, request.Quantity);

            var response = new PlaceOrderResponse
            {
                OrderId = Guid.NewGuid().ToString(),
                Success = true,
                Message = "Your order has been placed."
            };

            return Task.FromResult(response);
        }
    }
}
