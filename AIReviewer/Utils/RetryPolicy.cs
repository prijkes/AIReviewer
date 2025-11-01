using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace AIReviewer.Utils;

/// <summary>
/// Factory for creating retry policies with exponential backoff for transient failures.
/// </summary>
public sealed class RetryPolicyFactory
{
    private readonly ILogger<RetryPolicyFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryPolicyFactory"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public RetryPolicyFactory(ILogger<RetryPolicyFactory> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates an async retry policy for HTTP operations with exponential backoff.
    /// Retries on common transient failures like HTTP errors, timeouts, and service exceptions.
    /// </summary>
    /// <param name="component">Name of the component for logging purposes.</param>
    /// <returns>An async retry policy configured for HTTP operations.</returns>
    public AsyncRetryPolicy CreateHttpRetryPolicy(string component)
    {
        var delays = Backoff.ExponentialBackoff(TimeSpan.FromSeconds(2), 5, TimeSpan.FromSeconds(30));
        return Policy.Handle<Exception>(ex =>
            ex is HttpRequestException or TaskCanceledException or TimeoutException or Microsoft.VisualStudio.Services.Common.VssServiceException)
            .WaitAndRetryAsync(delays, (exception, timespan, retry, context) =>
            {
                _logger.LogWarning(exception, "{Component} transient failure (attempt {Retry}). Retrying in {Delay}s", component, retry, timespan.TotalSeconds);
            });
    }
}
