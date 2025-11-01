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

public sealed class AdoSdkClient : IDisposable
{
    private readonly ILogger<AdoSdkClient> _logger;
    private readonly ReviewerOptions _options;
    private readonly RetryPolicyFactory _retryFactory;
    private readonly VssConnection _connection;
    private readonly GitHttpClient _gitClient;

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

            return new PullRequestContext(pr, repo, commits.ToArray(), latestIteration);
        });
    }

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

    public async Task<GitPullRequestIterationChanges> GetIterationChangesAsync(Guid repoId, int prId, int iterationId, CancellationToken cancellationToken)
    {
        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(GitHttpClient));
        return await retry.ExecuteAsync(() => _gitClient.GetPullRequestIterationChangesAsync(repoId, prId, iterationId, cancellationToken: cancellationToken));
    }

    public GitHttpClient Git => _gitClient;

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

    private static string? TryGetIdentityProperty(Identity identity, string key)
    {
        if (identity.Properties != null && identity.Properties.TryGetValue(key, out var value) && value is string str && !string.IsNullOrWhiteSpace(str))
        {
            return str;
        }

        return null;
    }

    public void Dispose()
    {
        _gitClient.Dispose();
        _connection.Disconnect();
    }
}
