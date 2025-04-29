using Polly;
using Polly.CircuitBreaker;

namespace SagaOrchestrator.Policies
{
    public static class CircuitBreakerPolicy
    {
        public static AsyncCircuitBreakerPolicy CreateCircuitBreakerPolicy(ILogger logger)
        {
            return Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (ex, duration) =>
                    {
                        logger.LogWarning($"Circuit breaker opened. Duration: {duration}. Exception: {ex.Message}");
                    },
                    onReset: () =>
                    {
                        logger.LogInformation("Circuit breaker reset");
                    },
                    onHalfOpen: () =>
                    {
                        logger.LogInformation("Circuit breaker half-open");
                    }
                );
        }
    }
} 