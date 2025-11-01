using AIReviewer.Reviewer.Review;
using AIReviewer.Reviewer.AzureDevOps;
using AIReviewer.Reviewer.AzureDevOps.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AIReviewer.Reviewer.AzureDevOps;

public sealed class ApprovalService
{
    private readonly ILogger<ApprovalService> _logger;
    private readonly AdoSdkClient _adoClient;

    public ApprovalService(ILogger<ApprovalService> logger, AdoSdkClient adoClient)
    {
        _logger = logger;
        _adoClient = adoClient;
    }

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
