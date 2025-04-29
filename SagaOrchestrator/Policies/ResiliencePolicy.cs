using Polly;
namespace SagaOrchestrator.Policies
{
    public static class ResiliencePolicy
    {
        public static IAsyncPolicy CreateResiliencePolicy(ILogger logger)
        {
            // Retry Policy
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        logger.LogWarning($"Retry {retryCount} after {timeSpan.TotalSeconds} seconds due to: {exception.Message}");
                    });

            // Circuit Breaker Policy
            var circuitBreakerPolicy = CircuitBreakerPolicy.CreateCircuitBreakerPolicy(logger);

            // Timeout Policy
            var timeoutPolicy = TimeoutPolicy.CreateTimeoutPolicy(logger);

            // Combine all policies
            return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy, timeoutPolicy);
        }
    }
} 