using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace AIReviewer.Utils;

/// <summary>
/// Provides circuit breaker policies for protecting against cascading failures.
/// </summary>
public sealed class CircuitBreakerPolicyFactory(ILogger<CircuitBreakerPolicyFactory> logger)
{

    /// <summary>
    /// Creates a circuit breaker policy for AI API calls.
    /// Opens circuit after 3 consecutive failures, stays open for 30 seconds.
    /// </summary>
    public ResiliencePipeline CreateAiCircuitBreaker()
    {
        return new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5, // Open if 50% of requests fail
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 3, // Need at least 3 requests before evaluating
                BreakDuration = TimeSpan.FromSeconds(30),
                OnOpened = args =>
                {
                    logger.LogWarning("Circuit breaker opened due to failures. Blocking AI API calls for {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger.LogInformation("Circuit breaker closed. Resuming AI API calls");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    logger.LogInformation("Circuit breaker half-open. Testing if service has recovered");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a circuit breaker policy for Azure DevOps API calls.
    /// Opens circuit after 5 consecutive failures, stays open for 60 seconds.
    /// </summary>
    public ResiliencePipeline CreateAdoCircuitBreaker()
    {
        return new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(60),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(60),
                OnOpened = args =>
                {
                    logger.LogWarning("Circuit breaker opened for Azure DevOps API. Blocking calls for {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger.LogInformation("Circuit breaker closed for Azure DevOps API");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}
