using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AIReviewer.Reviewer.AzureDevOps.Models;

public sealed record PullRequestContext(
    GitPullRequest PullRequest,
    GitRepository Repository,
    GitCommitRef[] Commits,
    GitPullRequestIteration LatestIteration);
