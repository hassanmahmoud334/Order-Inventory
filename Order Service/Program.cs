using Order_Service.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddSingleton<Order_Service.Messaging.IMessageProducer, Order_Service.Messaging.RabbitMQProducer>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<OrderService>();
app.MapGet("/", () => "Order Service running - use gRPC client.");

app.Run();
