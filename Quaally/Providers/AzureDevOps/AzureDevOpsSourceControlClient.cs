using Quaally.AzureDevOps;
using Quaally.Core.Interfaces;
using Quaally.Core.Models;
using Quaally.Providers.AzureDevOps.Adapters;
using Microsoft.Extensions.Logging;

namespace Quaally.Providers.AzureDevOps;

/// <summary>
/// Azure DevOps implementation of ISourceControlClient.
/// Wraps IAdoSdkClient and adapts it to the generic interface.
/// </summary>
public sealed class AzureDevOpsSourceControlClient : ISourceControlClient
{
    private readonly ILogger<AzureDevOpsSourceControlClient> _logger;
    private readonly IAdoSdkClient _adoClient;

    public AzureDevOpsSourceControlClient(
        ILogger<AzureDevOpsSourceControlClient> logger,
        IAdoSdkClient adoClient)
    {
        _logger = logger;
        _adoClient = adoClient;
    }

    /// <inheritdoc/>
    public async Task<PullRequest> GetPullRequestAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting pull request information");
        
        var context = await _adoClient.GetPullRequestContextAsync(cancellationToken);
        var pr = ModelAdapter.ToPullRequest(context.PullRequest);
        
        _logger.LogDebug("Retrieved PR #{PrId}: {Title}", pr.Id, pr.Title);
        
        return pr;
    }

    /// <inheritdoc/>
    public async Task<List<FileChange>> GetFileChangesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting file changes");
        
        var context = await _adoClient.GetPullRequestContextAsync(cancellationToken);
        
        if (context.LatestIteration == null)
        {
            _logger.LogWarning("No iterations found for pull request");
            return new List<FileChange>();
        }

        var changes = await _adoClient.GetIterationChangesAsync(
            context.Repository.Id,
            context.PullRequest.PullRequestId,
            context.LatestIteration.Id ?? 1,
            cancellationToken);

        var fileChanges = changes.ChangeEntries?
            .Where(c => c.ChangeType != Microsoft.TeamFoundation.SourceControl.WebApi.VersionControlChangeType.None)
            .Select(c => new FileChange
            {
                Path = c.Item?.Path ?? string.Empty,
                ChangeType = ModelAdapter.ToFileChangeType(c.ChangeType),
                OriginalPath = c.SourceServerItem
            })
            .ToList() ?? new List<FileChange>();

        _logger.LogDebug("Found {Count} file changes", fileChanges.Count);
        
        return fileChanges;
    }

    /// <inheritdoc/>
    public async Task<string?> GetFileContentAsync(
        string filePath,
        string version,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting file content for {FilePath} at version {Version}", filePath, version);
        
        var item = await _adoClient.GetFileContentAsync(filePath, version);
        
        if (item?.Content == null)
        {
            _logger.LogWarning("File content not found for {FilePath}", filePath);
            return null;
        }

        return item.Content;
    }

    /// <inheritdoc/>
    public async Task<string?> GetFileDiffAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting diff for file {FilePath}", filePath);
        
        var context = await _adoClient.GetPullRequestContextAsync(cancellationToken);
        var baseCommit = context.PullRequest.LastMergeTargetCommit?.CommitId;
        var targetCommit = context.PullRequest.LastMergeSourceCommit?.CommitId;

        if (string.IsNullOrEmpty(baseCommit) || string.IsNullOrEmpty(targetCommit))
        {
            _logger.LogWarning("Cannot generate diff: missing base or target commit");
            return null;
        }

        var diff = await _adoClient.GetFileDiffAsync(filePath, baseCommit, targetCommit, cancellationToken);
        
        return diff;
    }

    /// <inheritdoc/>
    public async Task<string> SearchCodeAsync(
        string searchTerm,
        string? filePattern,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Searching code for '{SearchTerm}' with pattern '{FilePattern}', max results: {MaxResults}",
            searchTerm,
            filePattern ?? "all files",
            maxResults);
        
        var context = await _adoClient.GetPullRequestContextAsync(cancellationToken);
        var targetBranch = context.PullRequest.TargetRefName ?? "refs/heads/main";

        var results = await _adoClient.SearchCodeAsync(
            searchTerm,
            targetBranch,
            filePattern,
            maxResults);

        return System.Text.Json.JsonSerializer.Serialize(results);
    }

    /// <inheritdoc/>
    public async Task<string> GetFileHistoryAsync(
        string filePath,
        int maxCommits,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting file history for {FilePath}, max commits: {MaxCommits}", filePath, maxCommits);
        
        var context = await _adoClient.GetPullRequestContextAsync(cancellationToken);
        var targetBranch = context.PullRequest.TargetRefName ?? "refs/heads/main";

        var commits = await _adoClient.GetFileHistoryAsync(filePath, targetBranch, maxCommits);

        var history = commits.Select(c => new
        {
            commitId = c.CommitId,
            author = c.Author?.Name,
            date = c.Author?.Date,
            message = c.Comment
        }).ToList();

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            filePath,
            commitCount = history.Count,
            commits = history
        });
    }

    /// <inheritdoc/>
    public Task<UserIdentity> GetAuthenticatedUserAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting authenticated user identity");
        
        var identity = _adoClient.GetAuthorizedIdentity();
        var userIdentity = ModelAdapter.ToUserIdentity(identity);
        
        _logger.LogDebug("Authenticated as: {DisplayName} ({Id})", userIdentity.DisplayName, userIdentity.Id);
        
        return Task.FromResult(userIdentity);
    }
}
