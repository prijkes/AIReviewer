using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace AIReviewer.Utils;

public sealed class RetryPolicyFactory
{
    private readonly ILogger<RetryPolicyFactory> _logger;

    public RetryPolicyFactory(ILogger<RetryPolicyFactory> logger)
    {
        _logger = logger;
    }

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
