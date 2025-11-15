using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Quaaly.Infrastructure.AI;
using Quaaly.Infrastructure.AI.FunctionParameters;
using Quaaly.Infrastructure.AzureDevOps.Functions.Parameters;
using Quaaly.Infrastructure.Options;
using Quaaly.Infrastructure.Utils;
using System.Text.Json;

namespace Quaaly.Infrastructure.AzureDevOps.Functions;

/// <summary>
/// Executes Azure DevOps functions called by the AI.
/// Translates AI function requests into actual Azure DevOps API calls.
/// </summary>
public sealed class AzureDevOpsFunctionExecutor(
    ILogger<AzureDevOpsFunctionExecutor> logger,
    IAdoSdkClient adoClient,
    RetryPolicyFactory retryFactory,
    ReviewContextRetriever contextRetriever,
    IOptionsMonitor<ReviewerOptions> options)
{
    // PR Vote constants
    private const int VoteApproved = 10;
    private const int VoteApprovedWithSuggestions = 5;
    private const int VoteNoVote = 0;
    private const int VoteWaitForAuthor = -5;
    private const int VoteRejected = -10;
    
    private readonly ILogger<AzureDevOpsFunctionExecutor> _logger = logger;
    private readonly IAdoSdkClient _adoClient = adoClient;
    private readonly RetryPolicyFactory _retryFactory = retryFactory;
    private readonly ReviewContextRetriever _contextRetriever = contextRetriever;
    private readonly ReviewerOptions _options = options.CurrentValue;
    private Guid _repositoryId;
    private int _pullRequestId;

    /// <summary>
    /// Sets the current PR context for function execution.
    /// </summary>
    public void SetContext(Guid repositoryId, int pullRequestId)
    {
        _repositoryId = repositoryId;
        _pullRequestId = pullRequestId;
        _logger.LogDebug("Function executor context set: Repo={RepoId}, PR={PrId}", repositoryId, pullRequestId);
    }

    /// <summary>
    /// Executes a function by name with the provided arguments JSON.
    /// </summary>
    public async Task<string> ExecuteFunctionAsync(string functionName, string argumentsJson, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing function: {FunctionName} with args: {Args}", functionName, argumentsJson);

        try
        {
            var result = functionName switch
            {
                // Thread and Comment Management
                "create_pr_comment_thread" => await CreateThreadAsync(argumentsJson, cancellationToken),
                "reply_to_thread" => await ReplyToThreadAsync(argumentsJson, cancellationToken),
                "update_thread_status" => await UpdateThreadStatusAsync(argumentsJson, cancellationToken),
                "get_thread_conversation" => await GetThreadConversationAsync(argumentsJson, cancellationToken),

                // PR Status and Approval
                "approve_pull_request" => await ApprovePullRequestAsync(argumentsJson, cancellationToken),
                "complete_pull_request" => await CompletePullRequestAsync(argumentsJson, cancellationToken),
                "abandon_pull_request" => await AbandonPullRequestAsync(argumentsJson, cancellationToken),
                "set_auto_complete" => await SetAutoCompleteAsync(argumentsJson, cancellationToken),

                // PR Management
                "add_reviewer" => await AddReviewerAsync(argumentsJson, cancellationToken),
                "update_pr_description" => await UpdatePrDescriptionAsync(argumentsJson, cancellationToken),

                // PR Information Retrieval
                "get_pr_files" => await GetPrFilesAsync(argumentsJson, cancellationToken),
                "get_pr_diff" => await GetPrDiffAsync(argumentsJson, cancellationToken),
                "get_commit_details" => await GetCommitDetailsAsync(argumentsJson, cancellationToken),
                "get_pr_commits" => await GetPrCommitsAsync(argumentsJson, cancellationToken),
                "get_pr_work_items" => await GetPrWorkItemsAsync(argumentsJson, cancellationToken),

                // Code Analysis Functions
                "get_full_file_content" => await GetFullFileContentAsync(argumentsJson, cancellationToken),
                "get_file_at_commit" => await GetFileAtCommitAsync(argumentsJson, cancellationToken),
                "search_codebase" => await SearchCodebaseAsync(argumentsJson, cancellationToken),
                "get_related_files" => await GetRelatedFilesAsync(argumentsJson, cancellationToken),
                "get_file_history" => await GetFileHistoryAsync(argumentsJson, cancellationToken),

                _ => throw new NotImplementedException($"Function '{functionName}' is not implemented")
            };

            _logger.LogDebug("Function {FunctionName} completed successfully", functionName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return $"Error: {ex.Message}";
        }
    }

    // Thread and Comment Management Implementation

    private async Task<string> CreateThreadAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<CreateThreadParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        var thread = new GitPullRequestCommentThread
        {
            Comments =
            [
                new Comment { Content = args.CommentText, CommentType = CommentType.Text }
            ],
            Status = ParseThreadStatus(args.Status)
        };

        // Add file context if provided
        if (!string.IsNullOrEmpty(args.FilePath) && args.LineStart.HasValue)
        {
            thread.ThreadContext = new CommentThreadContext
            {
                FilePath = args.FilePath.StartsWith('/') ? args.FilePath : $"/{args.FilePath}",
                RightFileStart = new CommentPosition { Line = args.LineStart.Value, Offset = 1 },
                RightFileEnd = new CommentPosition { Line = args.LineEnd ?? args.LineStart.Value, Offset = 1 }
            };
        }

        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        var created = await retry.ExecuteAsync(() =>
            _adoClient.Git.CreateThreadAsync(thread, _repositoryId, _pullRequestId, cancellationToken: cancellationToken));

        return $"Thread created with ID: {created.Id}";
    }

    private async Task<string> ReplyToThreadAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<ReplyToThreadParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        var comment = new Comment
        {
            Content = args.CommentText,
            CommentType = CommentType.Text
        };

        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        await retry.ExecuteAsync(() =>
            _adoClient.Git.CreateCommentAsync(comment, _repositoryId, _pullRequestId, args.ThreadId, cancellationToken: cancellationToken));

        return $"Reply added to thread {args.ThreadId}";
    }

    private async Task<string> UpdateThreadStatusAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<UpdateThreadStatusParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        var updateThread = new GitPullRequestCommentThread
        {
            Status = ParseThreadStatus(args.Status)
        };

        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        await retry.ExecuteAsync(() =>
            _adoClient.Git.UpdateThreadAsync(updateThread, _repositoryId, _pullRequestId, args.ThreadId, cancellationToken: cancellationToken));

        return $"Thread {args.ThreadId} status updated to {args.Status}";
    }

    private async Task<string> GetThreadConversationAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<GetThreadConversationParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        var threads = await retry.ExecuteAsync(() =>
            _adoClient.Git.GetThreadsAsync(_repositoryId, _pullRequestId, cancellationToken: cancellationToken));

        var thread = threads.FirstOrDefault(t => t.Id == args.ThreadId);
        if (thread == null)
        {
            return JsonSerializer.Serialize(new { error = $"Thread {args.ThreadId} not found" });
        }

        var conversation = thread.Comments?
            .OrderBy(c => c.PublishedDate)
            .Select(c => $"[{c.Author?.DisplayName}]: {c.Content}")
            .ToList() ?? [];

        return JsonSerializer.Serialize(new { threadId = args.ThreadId, comments = conversation });
    }

    // PR Status and Approval Implementation

    private async Task<string> ApprovePullRequestAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<ApprovePullRequestParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        var identity = _adoClient.GetAuthorizedIdentity();
        var reviewer = new IdentityRefWithVote { Id = identity.Id, Vote = (short)args.Vote };

        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        await retry.ExecuteAsync(() =>
            _adoClient.Git.CreatePullRequestReviewerAsync(reviewer, _repositoryId, _pullRequestId, identity.Id.ToString(), cancellationToken: cancellationToken));

        // Add comment if provided
        if (!string.IsNullOrWhiteSpace(args.Comment))
        {
            var commentThread = new GitPullRequestCommentThread
            {
                Comments =
                [
                    new Comment { Content = args.Comment, CommentType = CommentType.Text }
                ],
                Status = CommentThreadStatus.Active
            };
            await retry.ExecuteAsync(() =>
                _adoClient.Git.CreateThreadAsync(commentThread, _repositoryId, _pullRequestId, cancellationToken: cancellationToken));
        }

        var voteText = args.Vote switch
        {
            VoteApproved => "approved",
            VoteApprovedWithSuggestions => "approved with suggestions",
            VoteNoVote => "removed vote",
            VoteWaitForAuthor => "waiting for author",
            VoteRejected => "rejected",
            _ => $"voted with {args.Vote}"
        };

        return $"Pull request {voteText}";
    }

    private async Task<string> CompletePullRequestAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<CompletePullRequestParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        var pr = await retry.ExecuteAsync(() =>
            _adoClient.Git.GetPullRequestAsync(_repositoryId, _pullRequestId, cancellationToken: cancellationToken));

        pr.Status = PullRequestStatus.Completed;
        pr.LastMergeSourceCommit = new GitCommitRef { CommitId = pr.LastMergeSourceCommit.CommitId };
        pr.CompletionOptions = new GitPullRequestCompletionOptions
        {
            DeleteSourceBranch = args.DeleteSourceBranch,
            MergeCommitMessage = args.CompletionMessage
        };

        await retry.ExecuteAsync(() =>
            _adoClient.Git.UpdatePullRequestAsync(pr, _repositoryId, _pullRequestId, cancellationToken: cancellationToken));

        return "Pull request completed (merged)";
    }

    private async Task<string> AbandonPullRequestAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<AbandonPullRequestParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        var pr = await retry.ExecuteAsync(() =>
            _adoClient.Git.GetPullRequestAsync(_repositoryId, _pullRequestId, cancellationToken: cancellationToken));

        pr.Status = PullRequestStatus.Abandoned;

        await retry.ExecuteAsync(() =>
            _adoClient.Git.UpdatePullRequestAsync(pr, _repositoryId, _pullRequestId, cancellationToken: cancellationToken));

        return $"Pull request abandoned{(string.IsNullOrWhiteSpace(args.Comment) ? "" : $": {args.Comment}")}";
    }

    private async Task<string> SetAutoCompleteAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<SetAutoCompleteParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        var pr = await retry.ExecuteAsync(() =>
            _adoClient.Git.GetPullRequestAsync(_repositoryId, _pullRequestId, cancellationToken: cancellationToken));

        if (args.Enable)
        {
            var identity = _adoClient.GetAuthorizedIdentity();
            pr.AutoCompleteSetBy = new IdentityRef { Id = identity.Id.ToString() };
            pr.CompletionOptions = new GitPullRequestCompletionOptions
            {
                DeleteSourceBranch = args.DeleteSourceBranch ?? false,
                MergeCommitMessage = args.Message
            };
        }
        else
        {
            pr.AutoCompleteSetBy = null;
            pr.CompletionOptions = null;
        }

        await retry.ExecuteAsync(() =>
            _adoClient.Git.UpdatePullRequestAsync(pr, _repositoryId, _pullRequestId, cancellationToken: cancellationToken));

        return args.Enable ? "Auto-complete enabled" : "Auto-complete disabled";
    }

    // PR Management Implementation

    private async Task<string> AddReviewerAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<AddReviewerParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        var reviewer = new IdentityRefWithVote
        {
            Id = args.ReviewerId,
            IsRequired = args.IsRequired
        };

        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        await retry.ExecuteAsync(() =>
            _adoClient.Git.CreatePullRequestReviewerAsync(reviewer, _repositoryId, _pullRequestId, args.ReviewerId, cancellationToken: cancellationToken));

        return $"Reviewer {args.ReviewerId} added as {(args.IsRequired ? "required" : "optional")}";
    }

    private async Task<string> UpdatePrDescriptionAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<UpdatePullRequestDescriptionParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        var pr = await retry.ExecuteAsync(() =>
            _adoClient.Git.GetPullRequestAsync(_repositoryId, _pullRequestId, cancellationToken: cancellationToken));

        pr.Description = args.Description;

        await retry.ExecuteAsync(() =>
            _adoClient.Git.UpdatePullRequestAsync(pr, _repositoryId, _pullRequestId, cancellationToken: cancellationToken));

        return "Pull request description updated";
    }

    // PR Information Retrieval Implementation

    private async Task<string> GetPrFilesAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<GetPullRequestFilesParameters>(argsJson);

        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        
        // Get the latest iteration
        var iterations = await retry.ExecuteAsync(() =>
            _adoClient.Git.GetPullRequestIterationsAsync(_repositoryId, _pullRequestId, cancellationToken: cancellationToken));
        
        var latestIteration = iterations.OrderByDescending(i => i.Id).FirstOrDefault();
        if (latestIteration == null)
        {
            return JsonSerializer.Serialize(new { fileCount = 0, files = new List<object>() });
        }

        var changes = await retry.ExecuteAsync(() =>
            _adoClient.Git.GetPullRequestIterationChangesAsync(_repositoryId, _pullRequestId, latestIteration.Id ?? 1, cancellationToken: cancellationToken));

        var files = changes.ChangeEntries?
            .Where(c => c.ChangeType != VersionControlChangeType.None)
            .Take(args?.MaxFiles ?? int.MaxValue)
            .Select(c => new { path = c.Item?.Path, changeType = c.ChangeType.ToString() })
            .ToList();

        files ??= [.. new List<(string path, string changeType)>().Select(x => new { x.path, x.changeType })];

        return JsonSerializer.Serialize(new { fileCount = files.Count, files });
    }

    private async Task<string> GetPrDiffAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<GetPullRequestDiffParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        try
        {
            var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
            
            // Get PR to access commit info
            var pr = await retry.ExecuteAsync(() =>
                _adoClient.Git.GetPullRequestAsync(_repositoryId, _pullRequestId, cancellationToken: cancellationToken));

            var baseCommit = pr.LastMergeTargetCommit?.CommitId;
            var targetCommit = pr.LastMergeSourceCommit?.CommitId;

            if (string.IsNullOrEmpty(baseCommit) || string.IsNullOrEmpty(targetCommit))
            {
                return JsonSerializer.Serialize(new { error = "Cannot generate diff: missing base or target commit" });
            }

            // Get the diff using Azure DevOps API
            var diffText = await _adoClient.GetFileDiffAsync(args.FilePath, baseCommit, targetCommit, cancellationToken);

            if (string.IsNullOrEmpty(diffText))
            {
                return JsonSerializer.Serialize(new { error = $"No diff found for {args.FilePath}" });
            }

            // Truncate if too large (keep first part which usually has most relevant changes)
            if (diffText.Length > _options.FunctionCalling.MaxDiffSizeBytes)
            {
                diffText = diffText[.._options.FunctionCalling.MaxDiffSizeBytes] + "\n... (diff truncated)";
            }

            return JsonSerializer.Serialize(new
            {
                filePath = args.FilePath,
                diffSize = diffText.Length,
                diff = diffText
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to get diff: {ex.Message}" });
        }
    }

    private async Task<string> GetCommitDetailsAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<GetCommitDetailsParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        var commit = await retry.ExecuteAsync(() =>
            _adoClient.Git.GetCommitAsync(args.CommitId, _repositoryId, cancellationToken: cancellationToken));

        return JsonSerializer.Serialize(new
        {
            commitId = commit.CommitId,
            author = commit.Author?.Name,
            date = commit.Author?.Date,
            message = commit.Comment
        });
    }

    private async Task<string> GetPrCommitsAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<GetPullRequestCommitsParameters>(argsJson);

        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        var commits = await retry.ExecuteAsync(() =>
            _adoClient.Git.GetPullRequestCommitsAsync(_repositoryId, _pullRequestId, cancellationToken: cancellationToken));

        var commitList = commits
            .Take(args?.MaxCommits ?? int.MaxValue)
            .Select(c => new { commitId = c.CommitId, author = c.Author?.Name, message = c.Comment })
            .ToList();

        return JsonSerializer.Serialize(new { commitCount = commitList.Count, commits = commitList });
    }

    private async Task<string> GetPrWorkItemsAsync(string argsJson, CancellationToken cancellationToken)
    {
        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        var workItemRefs = await retry.ExecuteAsync(() =>
            _adoClient.Git.GetPullRequestWorkItemRefsAsync(_repositoryId, _pullRequestId, cancellationToken: cancellationToken));

        var workItems = workItemRefs.Select(wi => new { id = wi.Id, url = wi.Url }).ToList();

        return JsonSerializer.Serialize(new { workItemCount = workItems.Count, workItems });
    }

    // Code Analysis Functions Implementation

    private async Task<string> GetFullFileContentAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<GetFullFileContentParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        try
        {
            var content = await _contextRetriever.GetFullFileContentAsync(args.FilePath);
            return JsonSerializer.Serialize(new { filePath = args.FilePath, content, lineCount = content.Split('\n').Length });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to retrieve file: {ex.Message}" });
        }
    }

    private async Task<string> GetFileAtCommitAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<GetFileAtCommitParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        try
        {
            var content = await _contextRetriever.GetFileAtCommitAsync(args.FilePath, args.CommitOrBranch);
            return JsonSerializer.Serialize(new { filePath = args.FilePath, commitOrBranch = args.CommitOrBranch, content });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to retrieve file at commit: {ex.Message}" });
        }
    }

    private async Task<string> SearchCodebaseAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<SearchCodebaseParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        try
        {
            return await _contextRetriever.SearchCodebaseAsync(
                args.SearchTerm,
                args.FilePattern,
                args.MaxResults ?? FunctionDefaults.SearchDefaultMaxResults);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Search failed: {ex.Message}" });
        }
    }

    private async Task<string> GetRelatedFilesAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<GetRelatedFilesParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        try
        {
            return await _contextRetriever.GetRelatedFilesAsync(args.FilePath);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to find related files: {ex.Message}" });
        }
    }

    private async Task<string> GetFileHistoryAsync(string argsJson, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<GetFileHistoryParameters>(argsJson)
            ?? throw new InvalidOperationException("Failed to deserialize parameters");

        try
        {
            return await _contextRetriever.GetFileHistoryAsync(
                args.FilePath,
                args.MaxCommits ?? FunctionDefaults.FileHistoryDefaultMaxCommits);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Failed to retrieve file history: {ex.Message}" });
        }
    }

    // Helper Methods

    private static CommentThreadStatus ParseThreadStatus(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "active" => CommentThreadStatus.Active,
            "bydesign" => CommentThreadStatus.ByDesign,
            "closed" => CommentThreadStatus.Closed,
            "fixed" => CommentThreadStatus.Fixed,
            "pending" => CommentThreadStatus.Pending,
            "wontfix" => CommentThreadStatus.WontFix,
            _ => CommentThreadStatus.Active
        };
    }
}
