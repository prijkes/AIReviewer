using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Quaally.AzureDevOps.Models;
using Quaally.Review;

namespace Quaally.AzureDevOps;

/// <summary>
/// Service for managing pull request approvals based on review results.
/// Automatically approves or rejects PRs based on error and warning counts.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ApprovalService"/> class.
/// </remarks>
/// <param name="logger">Logger for diagnostic information.</param>
/// <param name="adoClient">Client for Azure DevOps operations.</param>
public sealed class ApprovalService(ILogger<ApprovalService> logger, IAdoSdkClient adoClient)
{

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
        var currentIdentity = adoClient.GetAuthorizedIdentity();
        var reviewers = await adoClient.Git.GetPullRequestReviewersAsync(pr.Repository.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken);
        var botReviewer = reviewers.FirstOrDefault(r => r.UniqueName == currentIdentity.UniqueName);

        short desiredVote = (short)(result.ErrorCount == 0 && result.WarningCount <= result.WarnBudget ? 10 : 0);
        var decision = desiredVote == 10 ? "APPROVE" : "WAIT_FOR_AUTHOR";

        logger.LogInformation("Approval decision: {Decision} (Errors: {Errors}, Warnings: {Warnings}/{Budget})",
            decision, result.ErrorCount, result.WarningCount, result.WarnBudget);

        if (botReviewer == null)
        {
            var reviewer = new IdentityRefWithVote
            {
                Id = currentIdentity.Id,
                Vote = desiredVote
            };
            await adoClient.Git.CreatePullRequestReviewerAsync(reviewer, pr.Repository.Id, pr.PullRequest.PullRequestId, currentIdentity.Id, cancellationToken: cancellationToken);
            logger.LogInformation("Created reviewer entry with vote {Vote}", desiredVote);
        }
        else if (botReviewer.Vote != desiredVote)
        {
            botReviewer.Vote = desiredVote;
            await adoClient.Git.UpdatePullRequestReviewerAsync(botReviewer, pr.Repository.Id, pr.PullRequest.PullRequestId, botReviewer.Id, cancellationToken: cancellationToken);
            logger.LogInformation("Updated reviewer vote to {Vote}", desiredVote);
        }
        else
        {
            logger.LogInformation("Reviewer vote already set to {Vote}", desiredVote);
        }
    }
}
