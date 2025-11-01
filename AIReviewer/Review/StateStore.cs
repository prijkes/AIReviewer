using AIReviewer.AzureDevOps.Models;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Review;

/// <summary>
/// Service for persisting review state and issue fingerprints.
/// Currently a placeholder for future state persistence features.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="StateStore"/> class.
/// </remarks>
/// <param name="logger">Logger for diagnostic information.</param>
public sealed class StateStore(ILogger<StateStore> logger)
{

    /// <summary>
    /// Persists issue fingerprints for future reference.
    /// Currently logs the count but does not implement persistent storage.
    /// </summary>
    /// <param name="issues">The issues to persist.</param>
    public void Persist(IEnumerable<ReviewIssue> issues)
    {
        logger.LogDebug("Persisting {Count} fingerprints", issues.Count());
    }
}
