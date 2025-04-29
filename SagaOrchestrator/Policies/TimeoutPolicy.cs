using Polly;
using Polly.Timeout;
using Microsoft.Extensions.Logging;

namespace SagaOrchestrator.Policies
{
    public static class TimeoutPolicy
    {
        public static AsyncTimeoutPolicy CreateTimeoutPolicy(ILogger logger)
        {
            return Policy
                .TimeoutAsync(
                    timeout: TimeSpan.FromSeconds(30),
                    timeoutStrategy: TimeoutStrategy.Pessimistic,
                    onTimeoutAsync: async (context, timespan, task) =>
                    {
                        logger.LogWarning($"Timeout after {timespan.TotalSeconds} seconds");
                        await Task.CompletedTask;
                    }
                );
        }
    }
} 