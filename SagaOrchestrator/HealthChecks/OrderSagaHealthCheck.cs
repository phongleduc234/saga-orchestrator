using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using SagaOrchestrator.Data;

namespace SagaOrchestrator.HealthChecks
{
    public class OrderSagaHealthCheck : IHealthCheck
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrderSagaHealthCheck> _logger;

        public OrderSagaHealthCheck(
            IServiceProvider serviceProvider,
            ILogger<OrderSagaHealthCheck> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<SagaDbContext>();

                // Kiểm tra số lượng saga đang ở trạng thái lỗi
                var failedSagas = await dbContext.OrderSagaStates
                    .Where(s => s.CurrentState == "OrderFailed" || 
                               s.CurrentState == "PaymentFailed" || 
                               s.CurrentState == "InventoryFailed")
                    .CountAsync(cancellationToken);

                // Kiểm tra số lượng saga đang ở trạng thái pending quá lâu
                var staleSagas = await dbContext.OrderSagaStates
                    .Where(s => s.Created < DateTime.UtcNow.AddHours(-1) && 
                               s.CurrentState != "OrderCompleted" && 
                               s.CurrentState != "OrderFailed")
                    .CountAsync(cancellationToken);

                var data = new Dictionary<string, object>
                {
                    { "FailedSagas", failedSagas },
                    { "StaleSagas", staleSagas }
                };

                if (failedSagas > 10 || staleSagas > 20)
                {
                    return HealthCheckResult.Degraded(
                        $"Saga health check failed: {failedSagas} failed sagas, {staleSagas} stale sagas",
                        null,
                        data);
                }

                return HealthCheckResult.Healthy("Saga health check passed", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking saga health");
                return HealthCheckResult.Unhealthy("Error checking saga health", ex);
            }
        }
    }
} 