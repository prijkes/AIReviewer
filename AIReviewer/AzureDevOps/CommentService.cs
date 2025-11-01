using AIReviewer.AzureDevOps.Models;
using AIReviewer.Review;
using AIReviewer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AIReviewer.AzureDevOps;

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
public sealed class CommentService(ILogger<CommentService> logger, AdoSdkClient adoClient, RetryPolicyFactory retryPolicy)
{

    /// <summary>Property key to identify bot-created threads.</summary>
    private const string BotProperty = "ai-bot";
    /// <summary>Property key to store issue fingerprints for tracking across iterations.</summary>
    private const string FingerprintProperty = "fingerprint";
    /// <summary>Identifier for the special state tracking thread.</summary>
    private const string StateThreadIdentifier = "ai-state";

    /// <summary>
    /// Applies the review results by creating, updating, or resolving comment threads on the pull request.
    /// </summary>
    /// <param name="pr">The pull request context.</param>
    /// <param name="iteration">The current PR iteration.</param>
    /// <param name="result">The review results containing identified issues.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ApplyReviewAsync(PullRequestContext pr, GitPullRequestIteration iteration, ReviewPlanResult result, CancellationToken cancellationToken)
    {
        var threads = await adoClient.Git.GetThreadsAsync(pr.Repository.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken);
        var botThreads = threads.Where(t => t.Properties?.ContainsKey(BotProperty) == true).ToList();

        foreach (var issue in result.Issues)
        {
            var existing = botThreads.FirstOrDefault(t => t.Properties?.TryGetValue(FingerprintProperty, out var fp) == true && fp.ToString() == issue.Fingerprint);
            if (existing != null)
            {
                await AppendToExistingThreadAsync(pr, existing, issue, cancellationToken);
            }
            else
            {
                await CreateThreadAsync(pr, iteration, issue, cancellationToken);
            }
        }

        await ResolveClearedThreadsAsync(pr, iteration, result, botThreads, cancellationToken);
        await UpsertStateThreadAsync(pr, result, botThreads, cancellationToken);
    }

    /// <summary>
    /// Creates a new comment thread for an issue that hasn't been reported before.
    /// </summary>
    /// <param name="pr">The pull request context.</param>
    /// <param name="iteration">The current PR iteration.</param>
    /// <param name="issue">The issue to create a thread for.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task CreateThreadAsync(PullRequestContext pr, GitPullRequestIteration iteration, ReviewIssue issue, CancellationToken cancellationToken)
    {
        var thread = new GitPullRequestCommentThread
        {
            ThreadContext = new CommentThreadContext
            {
                FilePath = issue.FilePath,
                RightFileStart = issue.Line > 0 ? new CommentPosition { Line = issue.Line } : null
            },
            Comments =
            [
                new Comment
                {
                    CommentType = CommentType.Text,
                    Content = FormatComment(issue)
                }
            ],
            Status = CommentThreadStatus.Active,
            Properties = new Microsoft.VisualStudio.Services.WebApi.PropertiesCollection
            {
                [BotProperty] = true,
                [FingerprintProperty] = issue.Fingerprint
            }
        };

        var retry = retryPolicy.CreateHttpRetryPolicy(nameof(GitHttpClient));
        await retry.ExecuteAsync(() => adoClient.Git.CreateThreadAsync(thread, pr.Repository.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken));
        logger.LogInformation("Created new comment thread for issue {FingerPrint}", issue.Fingerprint);
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

        if (thread.Status == CommentThreadStatus.Fixed || thread.Status == CommentThreadStatus.Closed)
        {
            thread.Status = CommentThreadStatus.Active;
        }

        thread.Properties ??= [];
        thread.Properties[FingerprintProperty] = issue.Fingerprint;

        thread.Comments.Add(new Comment
        {
            CommentType = CommentType.Text,
            Content = $"Re-triggered: {FormatComment(issue)}"
        });

        await retry.ExecuteAsync(() => adoClient.Git.UpdateThreadAsync(thread, pr.Repository.Id, thread.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken));
        logger.LogInformation("Appended to existing thread {ThreadId}", thread.Id);
    }

    /// <summary>
    /// Resolves threads for issues that no longer appear in the current review results.
    /// Marks them as fixed to indicate the issue has been addressed.
    /// </summary>
    /// <param name="pr">The pull request context.</param>
    /// <param name="iteration">The current PR iteration.</param>
    /// <param name="result">The current review results.</param>
    /// <param name="botThreads">All bot-created threads.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ResolveClearedThreadsAsync(PullRequestContext pr, GitPullRequestIteration iteration, ReviewPlanResult result, List<GitPullRequestCommentThread> botThreads, CancellationToken cancellationToken)
    {
        var remainingFingerprints = result.Issues.Select(i => i.Fingerprint).ToHashSet();
        var closed = new List<int>();

        foreach (var thread in botThreads)
        {
            if (thread.Properties?.TryGetValue(FingerprintProperty, out var property) == true && property is string fp)
            {
                if (!remainingFingerprints.Contains(fp))
                {
                    thread.Status = CommentThreadStatus.Fixed;
                    var retry = retryPolicy.CreateHttpRetryPolicy(nameof(GitHttpClient));
                    await retry.ExecuteAsync(() => adoClient.Git.UpdateThreadAsync(thread, pr.Repository.Id, thread.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken));
                    closed.Add(thread.Id);
                }
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
        var state = new
        {
            fingerprints = result.Issues.Select(i => new
            {
                i.Fingerprint,
                i.FilePath,
                i.Line,
                i.Severity
            }).ToArray(),
            updatedAt = DateTimeOffset.UtcNow
        };

        var content = $"<!-- {StateThreadIdentifier} -->\n```\n{JsonHelpers.Serialize(state)}\n```";

        var stateThread = botThreads.FirstOrDefault(t => t.Properties?.ContainsKey(StateThreadIdentifier) == true);
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
                Status = CommentThreadStatus.Closed,
                Properties = new Microsoft.VisualStudio.Services.WebApi.PropertiesCollection
                {
                    [BotProperty] = true,
                    [StateThreadIdentifier] = true
                }
            };

            await retry.ExecuteAsync(() => adoClient.Git.CreateThreadAsync(thread, pr.Repository.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken));
            logger.LogInformation("Created state thread for fingerprints");
        }
        else
        {
            stateThread.Comments[^1].Content = content;
            await retry.ExecuteAsync(() => adoClient.Git.UpdateThreadAsync(stateThread, pr.Repository.Id, stateThread.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken));
            logger.LogInformation("Updated state thread for fingerprints");
        }
    }

    /// <summary>
    /// Formats a review issue into a markdown comment for display on Azure DevOps.
    /// </summary>
    /// <param name="issue">The issue to format.</param>
    /// <returns>A markdown-formatted comment string.</returns>
    private static string FormatComment(ReviewIssue issue)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"🤖 AI Review — {issue.Category}/{issue.Severity}");
        builder.AppendLine();
        builder.AppendLine(issue.Rationale);
        builder.AppendLine();
        builder.AppendLine($"**Recommendation**: {issue.Recommendation}");
        if (!string.IsNullOrWhiteSpace(issue.FixExample))
        {
            builder.AppendLine();
            builder.AppendLine("```csharp");
            builder.AppendLine(issue.FixExample);
            builder.AppendLine("```");
        }

        builder.AppendLine();
        builder.AppendLine("_I’m a bot; reply here to discuss. Set `DRY_RUN=true` to preview without posting._");
        return builder.ToString();
    }
}
