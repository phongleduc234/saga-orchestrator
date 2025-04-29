using SagaOrchestrator.Handlers;

namespace SagaOrchestrator.Services
{
    public class DeadLetterQueueProcessor : BackgroundService
    {
        private readonly ILogger<DeadLetterQueueProcessor> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _retryInterval;

        public DeadLetterQueueProcessor(
            ILogger<DeadLetterQueueProcessor> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _retryInterval = TimeSpan.FromSeconds(300); // 5 minutes
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Dead Letter Queue Processor is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var deadLetterHandler = scope.ServiceProvider.GetRequiredService<IDeadLetterQueueHandler>();
                        await deadLetterHandler.ProcessDeadLetterMessages();
                    }
                    await Task.Delay(_retryInterval, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Dead Letter Queue Processor");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }

            _logger.LogInformation("Dead Letter Queue Processor is stopping.");
        }
    }
} 