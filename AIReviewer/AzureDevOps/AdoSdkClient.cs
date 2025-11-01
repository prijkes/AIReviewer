using AIReviewer.AI;
using AIReviewer.AzureDevOps.Models;
using AIReviewer.Options;
using AIReviewer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.WebApi;
using System.Text;

namespace AIReviewer.AzureDevOps;

/// <summary>
/// Client for interacting with Azure DevOps using the ADO SDK.
/// Provides methods to fetch pull request context, repositories, and iteration changes.
/// </summary>
public sealed class AdoSdkClient : IDisposable
{
    private readonly ILogger<AdoSdkClient> _logger;
    private readonly ReviewerOptions _options;
    private readonly RetryPolicyFactory _retryFactory;
    private readonly VssConnection _connection;
    private readonly GitHttpClient _gitClient;
    private readonly LocalGitProvider? _localGitProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdoSdkClient"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="options">Configuration options for the reviewer.</param>
    /// <param name="retryFactory">Factory for creating retry policies.</param>
    /// <param name="localGitProviderLogger">Logger for LocalGitProvider.</param>
    public AdoSdkClient(
        ILogger<AdoSdkClient> logger, 
        IOptionsMonitor<ReviewerOptions> options, 
        RetryPolicyFactory retryFactory,
        ILogger<LocalGitProvider> localGitProviderLogger)
    {
        _logger = logger;
        _options = options.CurrentValue;
        _options.Normalize();
        _retryFactory = retryFactory;

        var creds = new VssBasicCredential(string.Empty, _options.AdoAccessToken);
        _connection = new VssConnection(new Uri(_options.AdoCollectionUrl), creds);
        _gitClient = _connection.GetClient<GitHttpClient>();

        // Initialize local git provider if LOCAL_REPO_PATH is configured
        if (!string.IsNullOrWhiteSpace(_options.LocalRepoPath))
        {
            _localGitProvider = new LocalGitProvider(localGitProviderLogger, _options.LocalRepoPath);
            _logger.LogInformation("Using local filesystem for file operations from: {LocalRepoPath}", _options.LocalRepoPath);
        }
        else
        {
            _logger.LogInformation("LOCAL_REPO_PATH not configured, will use Azure DevOps API for file operations");
        }
    }

    /// <summary>
    /// Retrieves the complete pull request context including PR details, repository, commits, and iterations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A <see cref="PullRequestContext"/> containing all PR-related information.</returns>
    public async Task<PullRequestContext> GetPullRequestContextAsync(CancellationToken cancellationToken)
    {
        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));

        return await retry.ExecuteAsync(async () =>
        {
            var repo = await ResolveRepositoryAsync(cancellationToken);
            var pr = await ResolvePullRequestAsync(repo.Id, cancellationToken);
            var commits = await _gitClient.GetPullRequestCommitsAsync(repo.Id, pr.PullRequestId, cancellationToken: cancellationToken);
            var iterations = await _gitClient.GetPullRequestIterationsAsync(repo.Id, pr.PullRequestId, cancellationToken: cancellationToken);
            var latestIteration = iterations.OrderByDescending(i => i.Id).First();

            return new PullRequestContext(pr, repo, [.. commits], latestIteration);
        });
    }

    /// <summary>
    /// Resolves the Git repository by ID or name from configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The resolved <see cref="GitRepository"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when neither ADO_REPO_ID nor ADO_REPO_NAME is provided.</exception>
    private async Task<GitRepository> ResolveRepositoryAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.AdoRepoId))
        {
            return await _gitClient.GetRepositoryAsync(Guid.Parse(_options.AdoRepoId), cancellationToken: cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(_options.AdoRepoName))
        {
            return await _gitClient.GetRepositoryAsync(_options.AdoProject, _options.AdoRepoName, cancellationToken: cancellationToken);
        }

        throw new InvalidOperationException("ADO_REPO_ID or ADO_REPO_NAME must be supplied.");
    }

    /// <summary>
    /// Resolves the pull request by ID or by searching commits for BUILD_SOURCE_VERSION.
    /// </summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The resolved <see cref="GitPullRequest"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when PR cannot be resolved.</exception>
    private async Task<GitPullRequest> ResolvePullRequestAsync(Guid repoId, CancellationToken cancellationToken)
    {
        if (_options.AdoPullRequestId is { } prId)
        {
            return await _gitClient.GetPullRequestAsync(repoId, prId, cancellationToken: cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(_options.BuildSourceVersion))
        {
            var candidates = await _gitClient.GetPullRequestsAsync(
                repoId,
                new GitPullRequestSearchCriteria
                {
                    Status = PullRequestStatus.All
                },
                top: 50,
                skip: 0,
                cancellationToken: cancellationToken);

            foreach (var candidate in candidates)
            {
                var commits = await _gitClient.GetPullRequestCommitsAsync(repoId, candidate.PullRequestId, cancellationToken: cancellationToken);
                if (commits.Any(commit => string.Equals(commit.CommitId, _options.BuildSourceVersion, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate;
                }
            }
        }

        throw new InvalidOperationException("ADO_PR_ID not provided and could not be inferred.");
    }

    /// <summary>
    /// Retrieves the list of file changes for a specific PR iteration.
    /// </summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="prId">The pull request ID.</param>
    /// <param name="iterationId">The iteration ID.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The iteration changes containing modified files.</returns>
    public async Task<GitPullRequestIterationChanges> GetIterationChangesAsync(Guid repoId, int prId, int iterationId, CancellationToken cancellationToken)
    {
        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        return await retry.ExecuteAsync(() => _gitClient.GetPullRequestIterationChangesAsync(repoId, prId, iterationId, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Gets the underlying Git HTTP client for direct ADO API access.
    /// </summary>
    public GitHttpClient Git => _gitClient;

    /// <summary>
    /// Retrieves the authorized identity (bot user) for the current connection.
    /// </summary>
    /// <returns>An <see cref="IdentityRef"/> representing the bot identity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when identity cannot be resolved.</exception>
    public IdentityRef GetAuthorizedIdentity()
    {
        var identity = _connection.AuthorizedIdentity
                       ?? throw new InvalidOperationException("Unable to resolve authorized identity from VssConnection.");

        var uniqueName = TryGetIdentityProperty(identity, "UniqueName") ??
                         TryGetIdentityProperty(identity, "DisplayName") ??
                         identity.DisplayName;

        return new IdentityRef
        {
            Id = identity.Id.ToString(),
            DisplayName = identity.DisplayName,
            UniqueName = uniqueName
        };
    }

    /// <summary>
    /// Attempts to retrieve a property value from the identity object.
    /// </summary>
    /// <param name="identity">The identity object.</param>
    /// <param name="key">The property key to retrieve.</param>
    /// <returns>The property value if found and non-empty; otherwise null.</returns>
    private static string? TryGetIdentityProperty(Identity identity, string key)
    {
        if (identity.Properties != null && identity.Properties.TryGetValue(key, out var value) && value is string str && !string.IsNullOrWhiteSpace(str))
        {
            return str;
        }

        return null;
    }

    /// <summary>
    /// Retrieves the content of a file at a specific version (commit or branch).
    /// When LOCAL_REPO_PATH is configured, uses local filesystem. Otherwise uses Azure DevOps API.
    /// </summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="versionDescriptor">The version (commit SHA or branch ref).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The file item with content.</returns>
    /// <exception cref="InvalidOperationException">Thrown when LOCAL_REPO_PATH is configured but file cannot be retrieved locally.</exception>
    public async Task<GitItem?> GetFileContentAsync(Guid repoId, string filePath, string versionDescriptor, CancellationToken cancellationToken)
    {
        // Use local git provider if configured
        if (_localGitProvider != null)
        {
            var content = await _localGitProvider.GetFileContentAsync(filePath, versionDescriptor);
            if (content == null)
            {
                throw new InvalidOperationException(
                    $"File not found in local repository: {filePath} at {versionDescriptor}. " +
                    $"LOCAL_REPO_PATH is configured ({_options.LocalRepoPath}), so API fallback is disabled.");
            }

            // Return a GitItem-like object with the content
            return new GitItem
            {
                Path = filePath,
                Content = content,
                GitObjectType = GitObjectType.Blob
            };
        }

        // Use Azure DevOps API
        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        
        try
        {
            var gitVersionDescriptor = new GitVersionDescriptor
            {
                Version = versionDescriptor.StartsWith("refs/") ? versionDescriptor.Replace("refs/heads/", "") : versionDescriptor,
                VersionType = versionDescriptor.StartsWith("refs/") ? GitVersionType.Branch : GitVersionType.Commit
            };

            var item = await retry.ExecuteAsync(() =>
                _gitClient.GetItemAsync(
                    repoId,
                    filePath,
                    versionDescriptor: gitVersionDescriptor,
                    includeContent: true,
                    cancellationToken: cancellationToken));

            return item;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get file content for {FilePath} at {Version}", filePath, versionDescriptor);
            return null;
        }
    }

    /// <summary>
    /// Searches the codebase for files containing specific text.
    /// When LOCAL_REPO_PATH is configured, uses git grep. Otherwise uses Azure DevOps API.
    /// </summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="searchTerm">The text to search for.</param>
    /// <param name="versionDescriptor">The version (branch ref) to search in.</param>
    /// <param name="filePattern">Optional file pattern filter (e.g., "*.cs").</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of search results.</returns>
    public async Task<List<CodeSearchResult>> SearchCodeAsync(
        Guid repoId,
        string searchTerm,
        string versionDescriptor,
        string? filePattern,
        int maxResults,
        CancellationToken cancellationToken)
    {
        // Use local git grep if configured (much faster for large codebases)
        if (_localGitProvider != null)
        {
            _logger.LogDebug("Using local git grep to search for '{SearchTerm}'", searchTerm);
            return await _localGitProvider.SearchCodeAsync(searchTerm, versionDescriptor, filePattern, maxResults);
        }

        // Use Azure DevOps API
        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        var results = new List<CodeSearchResult>();

        try
        {
            var gitVersionDescriptor = new GitVersionDescriptor
            {
                Version = versionDescriptor.StartsWith("refs/") ? versionDescriptor.Replace("refs/heads/", "") : versionDescriptor,
                VersionType = GitVersionType.Branch
            };

            // Get all files in the repository
            var items = await retry.ExecuteAsync(() =>
                _gitClient.GetItemsAsync(
                    repoId,
                    scopePath: "/",
                    recursionLevel: VersionControlRecursionType.Full,
                    versionDescriptor: gitVersionDescriptor,
                    cancellationToken: cancellationToken));

            // Filter by file pattern if specified
            var filesToSearch = items.Where(i => i.GitObjectType == GitObjectType.Blob);
            if (!string.IsNullOrWhiteSpace(filePattern))
            {
                var pattern = filePattern.Replace("*", ".*").Replace("?", ".");
                filesToSearch = filesToSearch.Where(i => System.Text.RegularExpressions.Regex.IsMatch(i.Path, pattern));
            }

            // Search through files
            foreach (var file in filesToSearch) // Limit files to search
            {
                if (results.Count >= maxResults) break;

                try
                {
                    var content = await GetFileContentAsync(repoId, file.Path, versionDescriptor, cancellationToken);
                    if (content?.Content == null) continue;

                    var lines = content.Content.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                        {
                            // Get context (line before and after)
                            var contextLines = new StringBuilder();
                            if (i > 0) contextLines.AppendLine(lines[i - 1]);
                            contextLines.AppendLine(lines[i]);
                            if (i < lines.Length - 1) contextLines.AppendLine(lines[i + 1]);

                            results.Add(new CodeSearchResult(file.Path, i + 1, contextLines.ToString()));
                            
                            if (results.Count >= maxResults) break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to search file {FilePath}", file.Path);
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to search codebase for '{SearchTerm}'", searchTerm);
            return results;
        }
    }

    /// <summary>
    /// Gets the commit history for a specific file.
    /// When LOCAL_REPO_PATH is configured, uses git log. Otherwise uses Azure DevOps API.
    /// </summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="versionDescriptor">The version (branch ref) to get history from.</param>
    /// <param name="maxCommits">Maximum number of commits to return.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of commits that modified the file.</returns>
    public async Task<List<GitCommitRef>> GetFileHistoryAsync(
        Guid repoId,
        string filePath,
        string versionDescriptor,
        int maxCommits,
        CancellationToken cancellationToken)
    {
        // Use local git log if configured
        if (_localGitProvider != null)
        {
            _logger.LogDebug("Using local git log to get history for {FilePath}", filePath);
            var history = await _localGitProvider.GetFileHistoryAsync(filePath, versionDescriptor, maxCommits);
            
            // Convert string history to GitCommitRef for compatibility
            // Note: Local provider returns simplified string format
            return history.Select(h => new GitCommitRef { Comment = h }).ToList();
        }

        // Use Azure DevOps API
        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));

        try
        {
            var gitVersionDescriptor = new GitVersionDescriptor
            {
                Version = versionDescriptor.StartsWith("refs/") ? versionDescriptor.Replace("refs/heads/", "") : versionDescriptor,
                VersionType = GitVersionType.Branch
            };

            var commits = await retry.ExecuteAsync(() =>
                _gitClient.GetCommitsAsync(
                    repoId,
                    new GitQueryCommitsCriteria
                    {
                        ItemPath = filePath,
                        ItemVersion = gitVersionDescriptor
                    },
                    top: maxCommits,
                    cancellationToken: cancellationToken));

            return commits.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get file history for {FilePath}", filePath);
            return [];
        }
    }

    /// <summary>
    /// Disposes the ADO client and disconnects the VSS connection.
    /// </summary>
    public void Dispose()
    {
        _gitClient.Dispose();
        _connection.Disconnect();
    }
}
