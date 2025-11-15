using Quaally.AzureDevOps;
using Quaally.Core.Interfaces;
using Quaally.Providers.AzureDevOps.Adapters;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Quaally.Providers.AzureDevOps;

/// <summary>
/// Azure DevOps implementation of IApprovalService.
/// Manages pull request approvals and status.
/// </summary>
public sealed class AzureDevOpsApprovalService : IApprovalService
{
    private readonly ILogger<AzureDevOpsApprovalService> _logger;
    private readonly IAdoSdkClient _adoClient;
    private Guid _repositoryId;
    private int _pullRequestId;

    // PR Vote constants
    private const int VoteApproved = 10;
    private const int VoteApprovedWithSuggestions = 5;
    private const int VoteNoVote = 0;
    private const int VoteWaitForAuthor = -5;
    private const int VoteRejected = -10;

    public AzureDevOpsApprovalService(
        ILogger<AzureDevOpsApprovalService> logger,
        IAdoSdkClient adoClient)
    {
        _logger = logger;
        _adoClient = adoClient;
    }

    /// <summary>
    /// Sets the context for approval operations.
    /// </summary>
    public void SetContext(Guid repositoryId, int pullRequestId)
    {
        _repositoryId = repositoryId;
        _pullRequestId = pullRequestId;
        _logger.LogDebug("Approval service context set: Repo={RepoId}, PR={PrId}", repositoryId, pullRequestId);
    }

    /// <inheritdoc/>
    public async Task VoteAsync(int vote, string? comment = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Voting on PR {PrId} with vote {Vote}", _pullRequestId, vote);

        var identity = _adoClient.GetAuthorizedIdentity();
        var reviewer = new IdentityRefWithVote { Id = identity.Id, Vote = (short)vote };

        await _adoClient.Git.CreatePullRequestReviewerAsync(
            reviewer,
            _repositoryId,
            _pullRequestId,
            identity.Id.ToString(),
            cancellationToken: cancellationToken);

        // Add comment if provided
        if (!string.IsNullOrWhiteSpace(comment))
        {
            var commentThread = new GitPullRequestCommentThread
            {
                Comments =
                [
                    new Comment { Content = comment, CommentType = CommentType.Text }
                ],
                Status = CommentThreadStatus.Active
            };
            
            await _adoClient.Git.CreateThreadAsync(
                commentThread,
                _repositoryId,
                _pullRequestId,
                cancellationToken: cancellationToken);
        }

        var voteText = vote switch
        {
            VoteApproved => "approved",
            VoteApprovedWithSuggestions => "approved with suggestions",
            VoteNoVote => "removed vote",
            VoteWaitForAuthor => "waiting for author",
            VoteRejected => "rejected",
            _ => $"voted with {vote}"
        };

        _logger.LogInformation("Pull request {VoteText}", voteText);
    }

    /// <inheritdoc/>
    public async Task CompletePullRequestAsync(
        bool deleteSourceBranch = false,
        string? completionMessage = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Completing PR {PrId}, delete source branch: {DeleteBranch}",
            _pullRequestId,
            deleteSourceBranch);

        var pr = await _adoClient.Git.GetPullRequestAsync(
            _repositoryId,
            _pullRequestId,
            cancellationToken: cancellationToken);

        pr.Status = PullRequestStatus.Completed;
        pr.LastMergeSourceCommit = new GitCommitRef { CommitId = pr.LastMergeSourceCommit.CommitId };
        pr.CompletionOptions = new GitPullRequestCompletionOptions
        {
            DeleteSourceBranch = deleteSourceBranch,
            MergeCommitMessage = completionMessage
        };

        await _adoClient.Git.UpdatePullRequestAsync(
            pr,
            _repositoryId,
            _pullRequestId,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Pull request completed (merged)");
    }

    /// <inheritdoc/>
    public async Task AbandonPullRequestAsync(string? comment = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Abandoning PR {PrId}", _pullRequestId);

        var pr = await _adoClient.Git.GetPullRequestAsync(
            _repositoryId,
            _pullRequestId,
            cancellationToken: cancellationToken);

        pr.Status = PullRequestStatus.Abandoned;

        await _adoClient.Git.UpdatePullRequestAsync(
            pr,
            _repositoryId,
            _pullRequestId,
            cancellationToken: cancellationToken);

        // Add comment if provided
        if (!string.IsNullOrWhiteSpace(comment))
        {
            var commentThread = new GitPullRequestCommentThread
            {
                Comments =
                [
                    new Comment { Content = comment, CommentType = CommentType.Text }
                ],
                Status = CommentThreadStatus.Active
            };
            
            await _adoClient.Git.CreateThreadAsync(
                commentThread,
                _repositoryId,
                _pullRequestId,
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation("Pull request abandoned");
    }

    /// <inheritdoc/>
    public async Task SetAutoCompleteAsync(
        bool enable,
        bool deleteSourceBranch = false,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Setting auto-complete to {Enable} for PR {PrId}",
            enable,
            _pullRequestId);

        var pr = await _adoClient.Git.GetPullRequestAsync(
            _repositoryId,
            _pullRequestId,
            cancellationToken: cancellationToken);

        if (enable)
        {
            var identity = _adoClient.GetAuthorizedIdentity();
            pr.AutoCompleteSetBy = new IdentityRef { Id = identity.Id.ToString() };
            pr.CompletionOptions = new GitPullRequestCompletionOptions
            {
                DeleteSourceBranch = deleteSourceBranch,
                MergeCommitMessage = message
            };
        }
        else
        {
            pr.AutoCompleteSetBy = null;
            pr.CompletionOptions = null;
        }

        await _adoClient.Git.UpdatePullRequestAsync(
            pr,
            _repositoryId,
            _pullRequestId,
            cancellationToken: cancellationToken);

        _logger.LogInformation(enable ? "Auto-complete enabled" : "Auto-complete disabled");
    }

    /// <inheritdoc/>
    public async Task AddReviewerAsync(
        string reviewerId,
        bool isRequired,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Adding reviewer {ReviewerId} as {Type} to PR {PrId}",
            reviewerId,
            isRequired ? "required" : "optional",
            _pullRequestId);

        var reviewer = new IdentityRefWithVote
        {
            Id = reviewerId,
            IsRequired = isRequired
        };

        await _adoClient.Git.CreatePullRequestReviewerAsync(
            reviewer,
            _repositoryId,
            _pullRequestId,
            reviewerId,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Reviewer added successfully");
    }

    /// <inheritdoc/>
    public async Task UpdateDescriptionAsync(string description, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating description for PR {PrId}", _pullRequestId);

        var pr = await _adoClient.Git.GetPullRequestAsync(
            _repositoryId,
            _pullRequestId,
            cancellationToken: cancellationToken);

        pr.Description = description;

        await _adoClient.Git.UpdatePullRequestAsync(
            pr,
            _repositoryId,
            _pullRequestId,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Pull request description updated");
    }
}
