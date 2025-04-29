using MassTransit;
using Microsoft.EntityFrameworkCore;
using SagaOrchestrator.Data;
using SagaOrchestrator.Handlers;
using SagaOrchestrator.Models;
using SagaOrchestrator.Services;
using SagaOrchestrator.StateMachines;
using SharedContracts.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

// Configure Entity Framework and PostgreSQL for saga state persistence
builder.Services.AddDbContext<SagaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register HTTP client for potential API calls
builder.Services.AddHttpClient();

// Register controllers for potential API endpoints
builder.Services.AddControllers();

// Register Dead Letter Queue services
builder.Services.AddScoped<IDeadLetterQueueHandler, DeadLetterQueueHandler>();
builder.Services.AddHostedService<DeadLetterQueueProcessor>();

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

        // Configure endpoints for saga state machine
        cfg.ReceiveEndpoint("order-created", e =>
        {
            e.ConfigureSaga<OrderSagaState>(context);
            e.BindDeadLetterQueue("order-created-saga-dlq", "order-created-saga-dlx", x => x.Durable = true);
        });

        cfg.ReceiveEndpoint("payment-processed", e =>
        {
            e.ConfigureSaga<OrderSagaState>(context);
            e.BindDeadLetterQueue("payment-processed-dlq", "payment-processed-dlx", x => x.Durable = true);
        });

        cfg.ReceiveEndpoint("inventory-updated", e =>
        {
            e.ConfigureSaga<OrderSagaState>(context);
            e.BindDeadLetterQueue("inventory-updated-dlq", "inventory-updated-dlx", x => x.Durable = true);
        });

        cfg.ReceiveEndpoint("payment-compensated", e =>
        {
            e.ConfigureSaga<OrderSagaState>(context);
            e.BindDeadLetterQueue("payment-compensated-dlq", "payment-compensated-dlx", x => x.Durable = true);
        });

        cfg.ReceiveEndpoint("inventory-compensated", e =>
        {
            e.ConfigureSaga<OrderSagaState>(context);
            e.BindDeadLetterQueue("inventory-compensated-dlq", "inventory-compensated-dlx", x => x.Durable = true);
        });

        cfg.ReceiveEndpoint("order-compensated", e =>
        {
            e.ConfigureSaga<OrderSagaState>(context);
            e.BindDeadLetterQueue("order-compensated-dlq", "order-compensated-dlx", x => x.Durable = true);
        });

        // Global Dead Letter Queue handler
        cfg.ReceiveEndpoint("saga-global-dlq", e =>
        {
            e.Handler<DeadLetterMessage>(async context =>
            {
                var logger = context.GetPayload<ILogger<DeadLetterMessage>>();
                logger.LogError("Dead-lettered message received: {MessageId}", context.MessageId);
            });

            e.Bind("order-created-saga-dlq");
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