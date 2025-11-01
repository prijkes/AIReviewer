using AIReviewer.Reviewer.AzureDevOps.Models;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Reviewer.Review;

public sealed class StateStore
{
    private readonly ILogger<StateStore> _logger;

    public StateStore(ILogger<StateStore> logger)
    {
        _logger = logger;
    }

    public void Persist(IEnumerable<ReviewIssue> issues)
    {
        _logger.LogDebug("Persisting {Count} fingerprints", issues.Count());
    }
}
