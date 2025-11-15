using Quaally.AzureDevOps;
using Quaally.Diff;
using Quaally.Options;
using Quaally.Review;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Quaally;

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
    IAdoSdkClient adoClient,
    DiffService diffService,
    ReviewPlanner planner,
    CommentService commentService,
    ApprovalService approvalService,
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
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            logger.LogInformation("Starting AI PR review (DryRun: {DryRun}, OnlyReviewIfRequiredReviewer: {OnlyReviewIfRequiredReviewer})",
                _options.DryRun, _options.OnlyReviewIfRequiredReviewer);

            var pr = await adoClient.GetPullRequestContextAsync(cancellationToken);

            // Check if bot must be a required reviewer
            if (_options.OnlyReviewIfRequiredReviewer)
            {
                var currentIdentity = adoClient.GetAuthorizedIdentity();
                var reviewers = await adoClient.Git.GetPullRequestReviewersAsync(
                    pr.Repository.Id,
                    pr.PullRequest.PullRequestId,
                    cancellationToken: cancellationToken);

                var botReviewer = reviewers.FirstOrDefault(r => r.UniqueName == currentIdentity.UniqueName);
                var isRequiredReviewer = botReviewer?.IsRequired ?? false;

                if (!isRequiredReviewer)
                {
                    logger.LogInformation(
                        "Skipping review: OnlyReviewIfRequiredReviewer is enabled and bot user '{BotUser}' is not a required reviewer on this PR",
                        currentIdentity.UniqueName);
                    return;
                }

                logger.LogInformation("Bot user '{BotUser}' is a required reviewer, proceeding with review", currentIdentity.UniqueName);
            }

            logger.LogInformation(
                "Loaded PR {Id} - {Title} [{Source} -> {Target}]",
                pr.PullRequest.PullRequestId,
                pr.PullRequest.Title,
                pr.PullRequest.SourceRefName,
                pr.PullRequest.TargetRefName);

            var iteration = pr.LatestIteration;

            // Check if this iteration has already been reviewed
            var threads = await adoClient.Git.GetThreadsAsync(pr.Repository.Id, pr.PullRequest.PullRequestId, cancellationToken: cancellationToken);
            var existingThreads = threads.Where(t => !t.IsDeleted).ToList();
            var botThreads = existingThreads.Where(t => t.IsCreatedByBot()).ToList();
            var lastReviewedIteration = await commentService.GetLastReviewedIterationAsync(botThreads);

            if (lastReviewedIteration.HasValue && lastReviewedIteration.Value == iteration.Id)
            {
                logger.LogInformation(
                    "Skipping review: Iteration {IterationId} has already been reviewed (last reviewed iteration: {LastReviewed})",
                    iteration.Id, lastReviewedIteration.Value);
                return;
            }

            logger.LogInformation("Reviewing iteration {IterationId} (last reviewed: {LastReviewed})",
                iteration.Id, lastReviewedIteration?.ToString() ?? "none");

            var diffs = await diffService.GetDiffsAsync(pr, iteration, cancellationToken);

            logger.LogInformation("PR has {CommitCount} commits, {FileCount} files to review", pr.Commits.Length, diffs.Count);

            // ReviewPlanner will load language-specific policies internally
            var reviewResult = await planner.PlanAsync(pr, iteration, diffs, existingThreads, _options.PolicyPath, cancellationToken);

            if (!_options.DryRun)
            {
                await commentService.ApplyReviewAsync(pr, iteration, reviewResult, botThreads, cancellationToken);
                await approvalService.ApplyApprovalAsync(pr, reviewResult, cancellationToken);
            }
            else
            {
                logger.LogWarning("DRY_RUN enabled: reporting issues without posting to Azure DevOps");
            }

            stopwatch.Stop();
            logger.LogInformation("Review completed in {ElapsedSeconds:F1}s - {TotalIssues} total issues ({Errors} errors, {Warnings} warnings, ShouldApprove: {ShouldApprove})", stopwatch.Elapsed.TotalSeconds, reviewResult.Issues.Count, reviewResult.ErrorCount, reviewResult.WarningCount, reviewResult.ShouldApprove);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI PR review failed");
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
