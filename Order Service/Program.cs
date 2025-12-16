using Microsoft.EntityFrameworkCore;
using Order_Service.Data;
using Order_Service.Outbox;
using Order_Service.Services;
using Order_Service.Messaging;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));
builder.Services.AddGrpc();
builder.Services.AddSingleton<IMessageProducer, RabbitMQProducer>();
builder.Services.AddHostedService<OutboxPublisher>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<OrderService>();
app.MapGet("/", () => "Order Service running - use gRPC client.");

app.Run();
