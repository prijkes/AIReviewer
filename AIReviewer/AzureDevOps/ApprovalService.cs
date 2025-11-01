using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using AIReviewer.AzureDevOps.Models;
using AIReviewer.Review;

namespace AIReviewer.AzureDevOps;

/// <summary>
/// Service for managing pull request approvals based on review results.
/// Automatically approves or rejects PRs based on error and warning counts.
/// </summary>
public sealed class ApprovalService
{
    private readonly ILogger<ApprovalService> _logger;
    private readonly AdoSdkClient _adoClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalService"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="adoClient">Client for Azure DevOps operations.</param>
    public ApprovalService(ILogger<ApprovalService> logger, AdoSdkClient adoClient)
    {
        _logger = logger;
        _adoClient = adoClient;
    }

    /// <summary>
    /// Applies approval or rejection to the pull request based on the review results.
    /// Approves (vote 10) if no errors and warnings are within budget; otherwise waits (vote 0).
    /// </summary>
    /// <param name="pr">The pull request context.</param>
    /// <param name="result">The review results containing error and warning counts.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ApplyApprovalAsync(PullRequestContext pr, ReviewPlanResult result, CancellationToken cancellationToken)
    {
        var currentIdentity = await _adoClient.Git.GetCurrentIdentityAsync(cancellationToken);
        var reviewers = await _adoClient.Git.GetReviewersAsync(pr.Repository.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken);
        var botReviewer = reviewers.FirstOrDefault(r => r.UniqueName == currentIdentity.UniqueName);

        var desiredVote = result.ErrorCount == 0 && result.WarningCount <= result.WarnBudget ? 10 : 0;

        if (botReviewer == null)
        {
            var reviewer = new IdentityRefWithVote
            {
                Id = currentIdentity.Id,
                Vote = desiredVote
            };
            await _adoClient.Git.CreatePullRequestReviewerAsync(reviewer, pr.Repository.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken);
            _logger.LogInformation("Created reviewer entry with vote {Vote}", desiredVote);
        }
        else if (botReviewer.Vote != desiredVote)
        {
            botReviewer.Vote = desiredVote;
            await _adoClient.Git.UpdatePullRequestReviewerAsync(botReviewer, pr.Repository.Id, pr.PullRequest.PullRequestId, botReviewer.Id, cancellationToken: cancellationToken);
            _logger.LogInformation("Updated reviewer vote to {Vote}", desiredVote);
        }
        else
        {
            _logger.LogInformation("Reviewer vote already set to {Vote}", desiredVote);
        }
    }
}
