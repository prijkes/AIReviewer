using AIReviewer.AzureDevOps.Models;
using AIReviewer.Options;
using AIReviewer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.WebApi;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="AdoSdkClient"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="options">Configuration options for the reviewer.</param>
    /// <param name="retryFactory">Factory for creating retry policies.</param>
    public AdoSdkClient(ILogger<AdoSdkClient> logger, IOptionsMonitor<ReviewerOptions> options, RetryPolicyFactory retryFactory)
    {
        _logger = logger;
        _options = options.CurrentValue;
        _options.Normalize();
        _retryFactory = retryFactory;

        var creds = new VssBasicCredential(string.Empty, _options.AdoAccessToken);
        _connection = new VssConnection(new Uri(_options.AdoCollectionUrl), creds);
        _gitClient = _connection.GetClient<GitHttpClient>();
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
    /// Disposes the ADO client and disconnects the VSS connection.
    /// </summary>
    public void Dispose()
    {
        _gitClient.Dispose();
        _connection.Disconnect();
    }
}
