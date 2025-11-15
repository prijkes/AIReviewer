using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace Quaally.Utils;

/// <summary>
/// Factory for creating retry policies with exponential backoff for transient failures.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RetryPolicyFactory"/> class.
/// </remarks>
/// <param name="logger">Logger for diagnostic information.</param>
public sealed class RetryPolicyFactory(ILogger<RetryPolicyFactory> logger)
{
    /// <summary>
    /// Creates an async retry policy for HTTP operations with exponential backoff.
    /// Retries on common transient failures like HTTP errors, timeouts, and service exceptions.
    /// </summary>
    /// <param name="component">Name of the component for logging purposes.</param>
    /// <returns>An async retry policy configured for HTTP operations.</returns>
    public AsyncRetryPolicy CreateHttpRetryPolicy(string component)
    {
        var delays = Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 5);
        return Polly.Policy.Handle<Exception>(ex =>
            ex is HttpRequestException or TaskCanceledException or TimeoutException or Microsoft.VisualStudio.Services.Common.VssServiceException)
            .WaitAndRetryAsync(delays, (exception, timespan, retry, context) =>
            {
                logger.LogWarning(exception, "{Component} transient failure (attempt {Retry}). Retrying in {Delay}s", component, retry, timespan.TotalSeconds);
            });
    }
}
