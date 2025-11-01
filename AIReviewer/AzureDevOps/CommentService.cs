using AIReviewer.AzureDevOps.Models;
using AIReviewer.Review;
using AIReviewer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AIReviewer.AzureDevOps;

public sealed class CommentService
{
    private readonly ILogger<CommentService> _logger;
    private readonly AdoSdkClient _adoClient;
    private readonly RetryPolicyFactory _retryPolicy;

    private const string BotProperty = "ai-bot";
    private const string FingerprintProperty = "fingerprint";
    private const string StateThreadIdentifier = "ai-state";

    public CommentService(ILogger<CommentService> logger, AdoSdkClient adoClient, RetryPolicyFactory retryPolicy)
    {
        _logger = logger;
        _adoClient = adoClient;
        _retryPolicy = retryPolicy;
    }

    public async Task ApplyReviewAsync(PullRequestContext pr, GitPullRequestIteration iteration, ReviewPlanResult result, CancellationToken cancellationToken)
    {
        var threads = await _adoClient.Git.GetThreadsAsync(pr.Repository.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken);
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

    private async Task CreateThreadAsync(PullRequestContext pr, GitPullRequestIteration iteration, ReviewIssue issue, CancellationToken cancellationToken)
    {
        var thread = new GitPullRequestCommentThread
        {
            ThreadContext = new CommentThreadContext
            {
                FilePath = issue.FilePath,
                RightFileStart = issue.Line > 0 ? new CommentPosition { Line = issue.Line } : null
            },
            Comments = new[]
            {
                new Comment
                {
                    CommentType = CommentType.Text,
                    Content = FormatComment(issue)
                }
            },
            Properties = new Dictionary<string, object>
            {
                [BotProperty] = true,
                [FingerprintProperty] = issue.Fingerprint
            },
            Status = CommentThreadStatus.Active,
            PullRequestThreadContext = new PullRequestThreadContext
            {
                IterationContext = new CommentIterationContext
                {
                    IterationId = iteration.Id,
                    FirstComparingIterationId = iteration.Id
                }
            }
        };

        var retry = _retryPolicy.CreateHttpRetryPolicy(nameof(GitHttpClient));
        await retry.ExecuteAsync(() => _adoClient.Git.CreateThreadAsync(thread, pr.Repository.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken));
        _logger.LogInformation("Created new comment thread for issue {FingerPrint}", issue.Fingerprint);
    }

    private async Task AppendToExistingThreadAsync(PullRequestContext pr, GitPullRequestCommentThread thread, ReviewIssue issue, CancellationToken cancellationToken)
    {
        var retry = _retryPolicy.CreateHttpRetryPolicy(nameof(GitHttpClient));

        if (thread.Status == CommentThreadStatus.Fixed || thread.Status == CommentThreadStatus.Closed)
        {
            thread.Status = CommentThreadStatus.Active;
        }

        thread.Properties ??= new Dictionary<string, object>();
        thread.Properties[FingerprintProperty] = issue.Fingerprint;

        thread.Comments.Add(new Comment
        {
            CommentType = CommentType.Text,
            Content = $"Re-triggered: {FormatComment(issue)}"
        });

        await retry.ExecuteAsync(() => _adoClient.Git.UpdateThreadAsync(thread, pr.Repository.Id, thread.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken));
        _logger.LogInformation("Appended to existing thread {ThreadId}", thread.Id);
    }

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
                    var retry = _retryPolicy.CreateHttpRetryPolicy(nameof(GitHttpClient));
                    await retry.ExecuteAsync(() => _adoClient.Git.UpdateThreadAsync(thread, pr.Repository.Id, thread.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken));
                    closed.Add(thread.Id);
                }
            }
        }

        if (closed.Count > 0)
        {
            _logger.LogInformation("Resolved {Count} threads: {Ids}", closed.Count, string.Join(",", closed));
        }
    }

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
        var retry = _retryPolicy.CreateHttpRetryPolicy(nameof(GitHttpClient));

        if (stateThread == null)
        {
            var thread = new GitPullRequestCommentThread
            {
                Comments = new[]
                {
                    new Comment
                    {
                        CommentType = CommentType.Text,
                        Content = content
                    }
                },
                Properties = new Dictionary<string, object>
                {
                    [BotProperty] = true,
                    [StateThreadIdentifier] = true
                },
                Status = CommentThreadStatus.Closed
            };

            await retry.ExecuteAsync(() => _adoClient.Git.CreateThreadAsync(thread, pr.Repository.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken));
            _logger.LogInformation("Created state thread for fingerprints");
        }
        else
        {
            stateThread.Comments[^1].Content = content;
            await retry.ExecuteAsync(() => _adoClient.Git.UpdateThreadAsync(stateThread, pr.Repository.Id, stateThread.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken));
            _logger.LogInformation("Updated state thread for fingerprints");
        }
    }

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
