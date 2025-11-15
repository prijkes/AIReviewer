using Quaally.AzureDevOps;
using Quaally.Core.Enums;
using Quaally.Core.Interfaces;
using Quaally.Providers.AzureDevOps.Adapters;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using AdoComment = Microsoft.TeamFoundation.SourceControl.WebApi.Comment;
using CoreComment = Quaally.Core.Models.Comment;
using CoreReviewThread = Quaally.Core.Models.ReviewThread;

namespace Quaally.Providers.AzureDevOps;

/// <summary>
/// Azure DevOps implementation of ICommentService.
/// Manages comments and threads on pull requests.
/// </summary>
public sealed class AzureDevOpsCommentService : ICommentService
{
    private readonly ILogger<AzureDevOpsCommentService> _logger;
    private readonly IAdoSdkClient _adoClient;
    private Guid _repositoryId;
    private int _pullRequestId;

    public AzureDevOpsCommentService(
        ILogger<AzureDevOpsCommentService> logger,
        IAdoSdkClient adoClient)
    {
        _logger = logger;
        _adoClient = adoClient;
    }

    /// <summary>
    /// Sets the context for comment operations.
    /// </summary>
    public void SetContext(Guid repositoryId, int pullRequestId)
    {
        _repositoryId = repositoryId;
        _pullRequestId = pullRequestId;
        _logger.LogDebug("Comment service context set: Repo={RepoId}, PR={PrId}", repositoryId, pullRequestId);
    }

    /// <inheritdoc/>
    public async Task<CoreReviewThread> CreateThreadAsync(
        string content,
        string? filePath = null,
        int? lineStart = null,
        int? lineEnd = null,
        ThreadStatus status = ThreadStatus.Active,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating thread on PR {PrId}: {FilePath} line {LineStart}",
            _pullRequestId,
            filePath ?? "general",
            lineStart);

        var thread = new GitPullRequestCommentThread
        {
            Comments =
            [
                new AdoComment { Content = content, CommentType = CommentType.Text }
            ],
            Status = ModelAdapter.FromThreadStatus(status)
        };

        // Add file context if provided
        if (!string.IsNullOrEmpty(filePath) && lineStart.HasValue)
        {
            thread.ThreadContext = new CommentThreadContext
            {
                FilePath = filePath.StartsWith('/') ? filePath : $"/{filePath}",
                RightFileStart = new CommentPosition { Line = lineStart.Value, Offset = 1 },
                RightFileEnd = new CommentPosition { Line = lineEnd ?? lineStart.Value, Offset = 1 }
            };
        }

        var created = await _adoClient.Git.CreateThreadAsync(
            thread,
            _repositoryId,
            _pullRequestId,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Thread created with ID: {ThreadId}", created.Id);

        return ModelAdapter.ToReviewThread(created);
    }

    /// <inheritdoc/>
    public async Task<CoreComment> ReplyToThreadAsync(
        int threadId,
        string content,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Replying to thread {ThreadId} on PR {PrId}", threadId, _pullRequestId);

        var comment = new AdoComment
        {
            Content = content,
            CommentType = CommentType.Text
        };

        var created = await _adoClient.Git.CreateCommentAsync(
            comment,
            _repositoryId,
            _pullRequestId,
            threadId,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Reply added to thread {ThreadId}", threadId);

        return ModelAdapter.ToComment(created);
    }

    /// <inheritdoc/>
    public async Task UpdateThreadStatusAsync(
        int threadId,
        ThreadStatus status,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating thread {ThreadId} status to {Status} on PR {PrId}",
            threadId,
            status,
            _pullRequestId);

        var updateThread = new GitPullRequestCommentThread
        {
            Status = ModelAdapter.FromThreadStatus(status)
        };

        await _adoClient.Git.UpdateThreadAsync(
            updateThread,
            _repositoryId,
            _pullRequestId,
            threadId,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Thread {ThreadId} status updated to {Status}", threadId, status);
    }

    /// <inheritdoc/>
    public async Task<List<CoreReviewThread>> GetThreadsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting all threads for PR {PrId}", _pullRequestId);

        var threads = await _adoClient.Git.GetThreadsAsync(
            _repositoryId,
            _pullRequestId,
            cancellationToken: cancellationToken);

        var reviewThreads = threads.Select(ModelAdapter.ToReviewThread).ToList();

        _logger.LogDebug("Retrieved {Count} threads", reviewThreads.Count);

        return reviewThreads;
    }

    /// <inheritdoc/>
    public async Task<CoreReviewThread?> GetThreadAsync(int threadId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting thread {ThreadId} for PR {PrId}", threadId, _pullRequestId);

        var threads = await GetThreadsAsync(cancellationToken);
        var thread = threads.FirstOrDefault(t => t.Id == threadId);

        if (thread == null)
        {
            _logger.LogWarning("Thread {ThreadId} not found", threadId);
        }

        return thread;
    }
}
