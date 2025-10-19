using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace PagueVeloz.Infrastructure.Resilience
{
    public static class PollyPolicies
    {
        public static AsyncRetryPolicy SqlRetryPolicy(ILogger logger)
        {
            return Policy
                .Handle<SqlException>()
                .Or<InvalidOperationException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        logger.LogWarning(exception, "Retry {RetryCount} after {Delay}s due to error: {Message}", retryCount, timeSpan.TotalSeconds, exception.Message);
                    });
        }
    }
}