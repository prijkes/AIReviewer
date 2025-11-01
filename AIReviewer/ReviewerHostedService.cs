using AIReviewer.AzureDevOps;
using AIReviewer.Diff;
using AIReviewer.Options;
using AIReviewer.Policy;
using AIReviewer.Review;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIReviewer;

/// <summary>
/// Hosted service that orchestrates the AI-powered pull request review process.
/// This is the main entry point that coordinates fetching PR data, analyzing diffs,
/// generating reviews, and posting results back to Azure DevOps.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ReviewerHostedService"/> class.
/// </remarks>
public sealed class ReviewerHostedService(
    ILogger<ReviewerHostedService> logger,
    IHostApplicationLifetime lifetime,
    AdoSdkClient adoClient,
    DiffService diffService,
    ReviewPlanner planner,
    CommentService commentService,
    ApprovalService approvalService,
    PolicyLoader policyLoader,
    IOptionsMonitor<ReviewerOptions> options) : IHostedService
{
    private readonly ReviewerOptions _options = options.CurrentValue;

    /// <summary>
    /// Starts the review process when the hosted service starts.
    /// Fetches PR context, analyzes diffs, generates AI reviews, and posts results.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope("PR:{Project}/{Repo}#{PR}", _options.AdoProject, _options.AdoRepoId ?? _options.AdoRepoName, _options.AdoPullRequestId);
        try
        {
            logger.LogInformation("Starting AI PR review (DryRun: {DryRun})", _options.DryRun);
            var policy = await policyLoader.LoadAsync(_options.PolicyPath, cancellationToken);

            var pr = await adoClient.GetPullRequestContextAsync(cancellationToken);
            logger.LogInformation("Loaded PR {Id} - {Title} [{Source} -> {Target}]", pr.PullRequest.PullRequestId, pr.PullRequest.Title, pr.PullRequest.SourceRefName, pr.PullRequest.TargetRefName);

            var iteration = pr.LatestIteration;
            var diffs = await diffService.GetDiffsAsync(pr, iteration, cancellationToken);

            var reviewResult = await planner.PlanAsync(pr, iteration, diffs, policy, cancellationToken);

            if (!_options.DryRun)
            {
                await commentService.ApplyReviewAsync(pr, iteration, reviewResult, cancellationToken);
                await approvalService.ApplyApprovalAsync(pr, reviewResult, cancellationToken);
            }
            else
            {
                logger.LogWarning("DRY_RUN enabled: reporting issues without posting to Azure DevOps");
            }

            logger.LogInformation("AI review completed with {ErrorCount} errors, {WarningCount} warnings", reviewResult.ErrorCount, reviewResult.WarningCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI PR review failed: {Message}", ex.Message);
            Environment.ExitCode = -1;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    /// <summary>
    /// Stops the hosted service. No cleanup needed as the application exits after review completion.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
