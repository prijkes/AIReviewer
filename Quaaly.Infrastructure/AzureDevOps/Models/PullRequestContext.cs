using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace Quaaly.Infrastructure.AzureDevOps.Models;

/// <summary>
/// Contains all contextual information about a pull request including its details, repository, commits, and iteration.
/// </summary>
/// <param name="PullRequest">The pull request object from Azure DevOps.</param>
/// <param name="Repository">The Git repository containing the pull request.</param>
/// <param name="Commits">Array of commits in the pull request.</param>
/// <param name="LatestIteration">The most recent iteration of the pull request.</param>
public sealed record PullRequestContext(
    GitPullRequest PullRequest,
    GitRepository Repository,
    GitCommitRef[] Commits,
    GitPullRequestIteration LatestIteration);
