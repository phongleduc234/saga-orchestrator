using MassTransit;
using Microsoft.EntityFrameworkCore;
using SagaOrchestrator.Data;
using SagaOrchestrator.StateMachines;
using SharedContracts.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure Entity Framework and PostgreSQL for saga state persistence
builder.Services.AddDbContext<SagaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register HTTP client for potential API calls
builder.Services.AddHttpClient();

// Register controllers for potential API endpoints
builder.Services.AddControllers();

// Configure MassTransit for saga orchestration
builder.Services.AddMassTransit(x =>
{
    // Register the saga state machine with Entity Framework persistence
    x.AddSagaStateMachine<OrderSaga, OrderSagaState>()
        .EntityFrameworkRepository(r =>
        {
            // Use pessimistic concurrency to prevent concurrent updates to the same saga
            r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
            // Use the existing DbContext
            r.ExistingDbContext<SagaDbContext>();
            // Use PostgreSQL for storage
            r.UsePostgres();
        });

    // Configure RabbitMQ as the message broker
    x.UsingRabbitMq((context, cfg) =>
    {
        // Get RabbitMQ configuration from appsettings
        var rabbitConfig = builder.Configuration.GetSection("RabbitMq");
        var host = rabbitConfig["Host"] ?? "localhost";
        var port = rabbitConfig.GetValue<int>("Port", 5672);
        var username = rabbitConfig["UserName"] ?? "guest";
        var password = rabbitConfig["Password"] ?? "guest";

        // Configure the RabbitMQ host
        cfg.Host(new Uri($"rabbitmq://{host}:{port}"), h =>
        {
            h.Username(username);
            h.Password(password);
        });

        // Configure global error handling and retry policies
        cfg.UseDelayedRedelivery(r => r.Intervals(
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15)
        ));
        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));

        // ENDPOINT CONFIGURATIONS FOR SAGA STATE MACHINE

        // 1. OrderCreated Event - Initiates the saga
        cfg.ReceiveEndpoint("order-created", e =>
        {
            e.ConfigureSaga<OrderSagaState>(context);
            e.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(1)));
            e.BindDeadLetterQueue("order-created-saga-dlq", "order-created-saga-dlx", x => {
                x.Durable = true;
            });
        });

        // 2. PaymentProcessed Event - The result of payment processing
        cfg.ReceiveEndpoint("payment-processed", e =>
        {
            e.ConfigureSaga<OrderSagaState>(context);
            e.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(1)));
            e.BindDeadLetterQueue("payment-processed-dlq", "payment-processed-dlx", x => {
                x.Durable = true;
            });
        });

        // 3. InventoryUpdated Event - The result of inventory reservation
        cfg.ReceiveEndpoint("inventory-updated", e =>
        {
            e.ConfigureSaga<OrderSagaState>(context);
            e.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(1)));
            e.BindDeadLetterQueue("inventory-updated-dlq", "inventory-updated-dlx", x => {
                x.Durable = true;
            });
        });

        // 4. PaymentCompensated Event - The result of payment compensation
        cfg.ReceiveEndpoint("payment-compensated", e =>
        {
            e.ConfigureSaga<OrderSagaState>(context);
            e.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(1)));
            e.BindDeadLetterQueue("payment-compensated-dlq", "payment-compensated-dlx", x => {
                x.Durable = true;
            });
        });

        // 5. InventoryCompensated Event - The result of inventory compensation
        cfg.ReceiveEndpoint("inventory-compensated", e =>
        {
            e.ConfigureSaga<OrderSagaState>(context);
            e.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(1)));
            e.BindDeadLetterQueue("inventory-compensated-dlq", "inventory-compensated-dlx", x => {
                x.Durable = true;
            });
        });

        // 6. OrderCompensated Event - The result of order compensation
        cfg.ReceiveEndpoint("order-compensated", e =>
        {
            e.ConfigureSaga<OrderSagaState>(context);
            e.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(1)));
            e.BindDeadLetterQueue("order-compensated-dlq", "order-compensated-dlx", x => {
                x.Durable = true;
            });
        });

        // Global Dead Letter Queue handler - Processes all dead-lettered messages
        cfg.ReceiveEndpoint("saga-global-dlq", e =>
        {
            // Simple handler to log all dead-lettered messages
            e.Handler<DeadLetterMessage>(async context =>
            {
                var logger = context.GetPayload<ILogger<DeadLetterMessage>>();
                logger.LogError("Dead-lettered message received: {MessageId}", context.MessageId);
                // Additional handling logic can be added here (alerts, manual intervention, etc.)
            });

            // Bind to all DLQs configured above
            e.Bind("order-created-saga-dlq");  // Note: Changed from "order-saga-dlq" for consistency
            e.Bind("payment-processed-dlq");
            e.Bind("inventory-updated-dlq");
            e.Bind("payment-compensated-dlq");
            e.Bind("inventory-compensated-dlq");
            e.Bind("order-compensated-dlq");
        });
    });
});

var app = builder.Build();

// Apply database migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SagaDbContext>();
    dbContext.Database.Migrate();
}

// Configure health checks endpoint
app.MapHealthChecks("/health");

// Configure routing and endpoints
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();