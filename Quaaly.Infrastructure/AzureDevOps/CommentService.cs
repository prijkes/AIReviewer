using Quaaly.Infrastructure.AzureDevOps.Models;
using Quaaly.Infrastructure.Review;
using Quaaly.Infrastructure.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace Quaaly.Infrastructure.AzureDevOps;

/// <summary>
/// Service for managing review comments and threads on Azure DevOps pull requests.
/// Handles creating, updating, and resolving comment threads based on AI-identified issues.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CommentService"/> class.
/// </remarks>
/// <param name="logger">Logger for diagnostic information.</param>
/// <param name="adoClient">Client for Azure DevOps operations.</param>
/// <param name="retryPolicy">Factory for creating retry policies.</param>
public sealed class CommentService(ILogger<CommentService> logger, IAdoSdkClient adoClient, RetryPolicyFactory retryPolicy)
{
    /// <summary>
    /// Gets the last reviewed iteration ID from existing bot threads.
    /// </summary>
    /// <param name="botThreads">The bot-created threads to analyze.</param>
    /// <returns>The highest iteration ID found, or null if no threads exist.</returns>
    public Task<int?> GetLastReviewedIterationAsync(List<GitPullRequestCommentThread> botThreads)
    {
        if (botThreads.Count == 0)
            return Task.FromResult<int?>(null);

        // Extract iteration IDs from thread properties
        var iterationIds = botThreads
            .Select(t => t.GetIterationId())
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        if (iterationIds.Count == 0)
            return Task.FromResult<int?>(null);

        var maxIteration = iterationIds.Max();
        logger.LogDebug("Last reviewed iteration: {IterationId} (from {ThreadCount} threads)", maxIteration, iterationIds.Count);

        return Task.FromResult<int?>(maxIteration);
    }

    /// <summary>
    /// Applies the review results by creating, updating, or resolving comment threads on the pull request.
    /// </summary>
    /// <param name="pr">The pull request context.</param>
    /// <param name="iteration">The current PR iteration.</param>
    /// <param name="result">The review results containing identified issues.</param>
    /// <param name="botThreads">Pre-fetched bot-created threads to avoid re-fetching.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ApplyReviewAsync(PullRequestContext pr, GitPullRequestIteration iteration, ReviewPlanResult result, List<GitPullRequestCommentThread> botThreads, CancellationToken cancellationToken)
    {
        logger.LogInformation("[THREAD-MATCHING] Using {BotThreads} pre-fetched bot threads", botThreads.Count);

        // Log all existing bot thread fingerprints
        foreach (var thread in botThreads)
        {
            var fp = thread.GetFingerprint();
            logger.LogInformation("[THREAD-MATCHING] Existing thread {ThreadId}: fingerprint={Fingerprint}, status={Status}",
                thread.Id, fp ?? "NULL", thread.Status);
        }

        var iterationId = iteration.Id ?? throw new InvalidOperationException("Iteration ID is required");

        foreach (var issue in result.Issues)
        {
            logger.LogInformation("[THREAD-MATCHING] Processing issue with fingerprint={Fingerprint} for {FilePath}",
                issue.Fingerprint, issue.FilePath);

            var existing = botThreads.FirstOrDefault(t => t.GetFingerprint() == issue.Fingerprint);
            if (existing != null)
            {
                logger.LogInformation("[THREAD-MATCHING] MATCHED existing thread {ThreadId} for issue {Fingerprint}",
                    existing.Id, issue.Fingerprint);
                await AppendToExistingThreadAsync(pr, existing, issue, cancellationToken);
            }
            else
            {
                logger.LogInformation("[THREAD-MATCHING] NO MATCH found, creating new thread for issue {Fingerprint}",
                    issue.Fingerprint);
                await CreateThreadAsync(pr, issue, iterationId, cancellationToken);
            }
        }

        await ResolveClearedThreadsAsync(pr, result, botThreads, cancellationToken);
        await UpsertStateThreadAsync(pr, result, botThreads, cancellationToken);
    }

    /// <summary>
    /// Creates a new comment thread for an issue that hasn't been reported before.
    /// For deleted files, positions the comment on the left side of the diff (the original content).
    /// For added/modified files, positions the comment on the right side of the diff (the new content).
    /// </summary>
    /// <param name="pr">The pull request context.</param>
    /// <param name="issue">The issue to create a thread for.</param>
    /// <param name="iterationId">The iteration ID when this issue was found.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task CreateThreadAsync(PullRequestContext pr, ReviewIssue issue, int iterationId, CancellationToken cancellationToken)
    {
        // For PR metadata comments (e.g., PR description issues), there's no file path
        var hasFile = !string.IsNullOrEmpty(issue.FilePath);
        
        // Log the issue details for debugging
        logger.LogInformation("[COMMENT-DEBUG] Creating thread for issue: FilePath={FilePath}, LineStart={LineStart}, StartOffset={StartOffset} LineEnd={LineEnd} EndOffset={EndOffset}, IsDeleted={IsDeleted}, HasFile={HasFile}", 
            issue.FilePath, issue.LineStart, issue.LineStartOffset, issue.LineEnd, issue.LineEndOffset, issue.IsDeleted, hasFile);
        
        var thread = new GitPullRequestCommentThread
        {
            ThreadContext = hasFile ? new CommentThreadContext
            {
                // Ensure file path starts with / for Azure DevOps to properly locate it in Files tab
                FilePath = issue.FilePath.StartsWith('/') ? issue.FilePath : $"/{issue.FilePath}",
                // For deleted files, position comment on left side (original content)
                // For added/modified files, position comment on right side (new content)
                // Both Start and End are required for comments to appear in Files tab
                // Use actual file line numbers from the AI (not diff line numbers)
                // Note: Azure DevOps requires offset >= 1 (1-based indexing), so we use Math.Max(1, offset)
                LeftFileStart = issue.IsDeleted && issue.LineStart > 0 ? new CommentPosition { Line = issue.LineStart, Offset = Math.Max(1, issue.LineStartOffset) } : null,
                LeftFileEnd = issue.IsDeleted && issue.LineEnd > 0 ? new CommentPosition { Line = issue.LineEnd, Offset = Math.Max(1, issue.LineEndOffset) } : null,
                RightFileStart = !issue.IsDeleted && issue.LineStart > 0 ? new CommentPosition { Line = issue.LineStart, Offset = Math.Max(1, issue.LineStartOffset) } : null,
                RightFileEnd = !issue.IsDeleted && issue.LineEnd > 0 ? new CommentPosition { Line = issue.LineEnd, Offset = Math.Max(1, issue.LineEndOffset) } : null
            } : null,
            PullRequestThreadContext = hasFile ? new GitPullRequestCommentThreadContext
            {
                // Set iteration context to track comments across PR iterations
                IterationContext = new CommentIterationContext
                {
                    // Compare from base iteration (1) to current iteration
                    FirstComparingIteration = 1,
                    SecondComparingIteration = (short)iterationId
                },
                // ChangeTrackingId is used to track the comment position across iterations
                // We use 1 as a default since we're creating a new thread
                ChangeTrackingId = 1
            } : null,
            Comments =
            [
                new Comment
                {
                    CommentType = CommentType.Text,
                    Content = CommentFormatter.FormatReviewIssue(issue)
                }
            ],
            Status = CommentThreadStatus.Active
        };

        thread.MarkAsBot();
        thread.SetFingerprint(issue.Fingerprint);
        thread.SetIterationId(iterationId);

        var retry = retryPolicy.CreateHttpRetryPolicy(nameof(GitHttpClient));
        await retry.ExecuteAsync(() => adoClient.Git.CreateThreadAsync(thread, pr.Repository.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken));
        logger.LogInformation("Created new comment thread for issue {FingerPrint} at iteration {IterationId}", issue.Fingerprint, iterationId);
    }

    /// <summary>
    /// Appends a new comment to an existing thread when the same issue is re-triggered.
    /// Reopens the thread if it was previously closed or fixed.
    /// </summary>
    /// <param name="pr">The pull request context.</param>
    /// <param name="thread">The existing thread to append to.</param>
    /// <param name="issue">The re-triggered issue.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task AppendToExistingThreadAsync(PullRequestContext pr, GitPullRequestCommentThread thread, ReviewIssue issue, CancellationToken cancellationToken)
    {
        var retry = retryPolicy.CreateHttpRetryPolicy(nameof(GitHttpClient));

        // Create a new thread object with only the fields we want to update
        // Only include NEW comments to append, not existing ones (which would have Author fields)
        var updateThread = new GitPullRequestCommentThread
        {
            Status = (thread.Status == CommentThreadStatus.Fixed || thread.Status == CommentThreadStatus.Closed)
                ? CommentThreadStatus.Active
                : thread.Status,
            Comments =
            [
                new Comment
                {
                    CommentType = CommentType.Text,
                    Content = CommentFormatter.FormatReTriggeredIssue(issue)
                }
            ]
        };

        await retry.ExecuteAsync(() => adoClient.Git.UpdateThreadAsync(updateThread, pr.Repository.Id, pr.PullRequest.PullRequestId, thread.Id, cancellationToken: cancellationToken));
        logger.LogInformation("Appended to existing thread {ThreadId}", thread.Id);
    }

    /// <summary>
    /// Resolves threads for issues that no longer appear in the current review results.
    /// Marks them as fixed to indicate the issue has been addressed.
    /// </summary>
    /// <param name="pr">The pull request context.</param>
    /// <param name="result">The current review results.</param>
    /// <param name="botThreads">All bot-created threads.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ResolveClearedThreadsAsync(PullRequestContext pr, ReviewPlanResult result, List<GitPullRequestCommentThread> botThreads, CancellationToken cancellationToken)
    {
        var remainingFingerprints = result.Issues.Select(i => i.Fingerprint).ToHashSet();
        var closed = new List<int>();

        foreach (var thread in botThreads)
        {
            var fingerprint = thread.GetFingerprint();
            if (fingerprint != null && !remainingFingerprints.Contains(fingerprint))
            {
                // Create a new thread object with only the Status field to update
                // This avoids sending Properties back to Azure DevOps which would cause an error
                var updateThread = new GitPullRequestCommentThread
                {
                    Status = CommentThreadStatus.Fixed
                };

                var retry = retryPolicy.CreateHttpRetryPolicy(nameof(GitHttpClient));
                await retry.ExecuteAsync(() => adoClient.Git.UpdateThreadAsync(updateThread, pr.Repository.Id, pr.PullRequest.PullRequestId, thread.Id, cancellationToken: cancellationToken));
                closed.Add(thread.Id);
            }
        }

        if (closed.Count > 0)
        {
            logger.LogInformation("Resolved {Count} threads: {Ids}", closed.Count, string.Join(",", closed));
        }
    }

    /// <summary>
    /// Creates or updates a special state thread that tracks all issue fingerprints.
    /// This hidden thread maintains a JSON record of all active issues.
    /// </summary>
    /// <param name="pr">The pull request context.</param>
    /// <param name="result">The current review results.</param>
    /// <param name="botThreads">All bot-created threads.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task UpsertStateThreadAsync(PullRequestContext pr, ReviewPlanResult result, List<GitPullRequestCommentThread> botThreads, CancellationToken cancellationToken)
    {
        var content = CommentFormatter.FormatStateThread(result);

        var stateThread = botThreads.FirstOrDefault(t => t.IsStateThread());
        var retry = retryPolicy.CreateHttpRetryPolicy(nameof(GitHttpClient));

        if (stateThread == null)
        {
            var thread = new GitPullRequestCommentThread
            {
                Comments =
                [
                    new Comment
                    {
                        CommentType = CommentType.Text,
                        Content = content
                    }
                ],
                Status = CommentThreadStatus.Closed
            };

            thread.MarkAsStateThread();

            await retry.ExecuteAsync(() => adoClient.Git.CreateThreadAsync(thread, pr.Repository.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken));
            logger.LogInformation("Created state thread for fingerprints");
        }
        else
        {
            // Update the existing comment's content using UpdateCommentAsync
            // Create a new comment object with only the Content field to avoid Author errors
            var commentId = stateThread.Comments[^1].Id;
            var updatedComment = new Comment
            {
                Content = content
            };

            await retry.ExecuteAsync(() => adoClient.Git.UpdateCommentAsync(
                updatedComment,
                pr.Repository.Id,
                pr.PullRequest.PullRequestId,
                stateThread.Id,
                commentId,
                cancellationToken: cancellationToken));
            logger.LogInformation("Updated state thread for fingerprints");
        }
    }
}
