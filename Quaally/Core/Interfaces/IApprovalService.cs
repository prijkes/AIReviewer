using Quaally.Core.Models;

namespace Quaally.Core.Interfaces;

/// <summary>
/// Interface for managing pull request approvals and status.
/// </summary>
public interface IApprovalService
{
    /// <summary>
    /// Votes on a pull request.
    /// </summary>
    /// <param name="vote">Vote value (provider-specific, typically -10 to +10).</param>
    /// <param name="comment">Optional comment to add with the vote.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task VoteAsync(int vote, string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes (merges) the pull request.
    /// </summary>
    /// <param name="deleteSourceBranch">Whether to delete the source branch after merge.</param>
    /// <param name="completionMessage">Optional merge commit message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task CompletePullRequestAsync(
        bool deleteSourceBranch = false,
        string? completionMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Abandons (closes without merging) the pull request.
    /// </summary>
    /// <param name="comment">Optional comment explaining why.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task AbandonPullRequestAsync(string? comment = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables auto-complete on the pull request.
    /// </summary>
    /// <param name="enable">True to enable, false to disable.</param>
    /// <param name="deleteSourceBranch">Whether to delete source branch on auto-complete.</param>
    /// <param name="message">Optional merge commit message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task SetAutoCompleteAsync(
        bool enable,
        bool deleteSourceBranch = false,
        string? message = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a reviewer to the pull request.
    /// </summary>
    /// <param name="reviewerId">ID of the reviewer to add.</param>
    /// <param name="isRequired">Whether the reviewer is required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task AddReviewerAsync(string reviewerId, bool isRequired, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the pull request description.
    /// </summary>
    /// <param name="description">New description text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task UpdateDescriptionAsync(string description, CancellationToken cancellationToken = default);
}
