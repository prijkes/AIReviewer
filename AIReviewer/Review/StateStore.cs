using AIReviewer.AzureDevOps.Models;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Review;

/// <summary>
/// Service for persisting review state and issue fingerprints.
/// Currently a placeholder for future state persistence features.
/// </summary>
public sealed class StateStore
{
    private readonly ILogger<StateStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateStore"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public StateStore(ILogger<StateStore> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Persists issue fingerprints for future reference.
    /// Currently logs the count but does not implement persistent storage.
    /// </summary>
    /// <param name="issues">The issues to persist.</param>
    public void Persist(IEnumerable<ReviewIssue> issues)
    {
        _logger.LogDebug("Persisting {Count} fingerprints", issues.Count());
    }
}
