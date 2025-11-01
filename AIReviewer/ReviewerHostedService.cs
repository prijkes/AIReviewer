using AIReviewer.Reviewer.AzureDevOps;
using AIReviewer.Reviewer.Diff;
using AIReviewer.Reviewer.Options;
using AIReviewer.Reviewer.Policy;
using AIReviewer.Reviewer.Review;
using AIReviewer.Reviewer.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIReviewer;

public sealed class ReviewerHostedService : IHostedService
{
    private readonly ILogger<ReviewerHostedService> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly AdoSdkClient _adoClient;
    private readonly DiffService _diffService;
    private readonly ReviewPlanner _planner;
    private readonly CommentService _commentService;
    private readonly ApprovalService _approvalService;
    private readonly PolicyLoader _policyLoader;
    private readonly ReviewerOptions _options;

    public ReviewerHostedService(
        ILogger<ReviewerHostedService> logger,
        IHostApplicationLifetime lifetime,
        AdoSdkClient adoClient,
        DiffService diffService,
        ReviewPlanner planner,
        CommentService commentService,
        ApprovalService approvalService,
        PolicyLoader policyLoader,
        IOptionsMonitor<ReviewerOptions> options)
    {
        _logger = logger;
        _lifetime = lifetime;
        _adoClient = adoClient;
        _diffService = diffService;
        _planner = planner;
        _commentService = commentService;
        _approvalService = approvalService;
        _policyLoader = policyLoader;
        _options = options.CurrentValue;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope("PR:{Project}/{Repo}#{PR}", _options.AdoProject, _options.AdoRepoId ?? _options.AdoRepoName, _options.AdoPullRequestId);
        try
        {
            _logger.LogInformation("Starting AI PR review (DryRun: {DryRun})", _options.DryRun);
            var policy = await _policyLoader.LoadAsync(_options.PolicyPath, cancellationToken);

            var pr = await _adoClient.GetPullRequestContextAsync(cancellationToken);
            _logger.LogInformation("Loaded PR {Id} - {Title} [{Source} -> {Target}]", pr.PullRequest.PullRequestId, pr.PullRequest.Title, pr.PullRequest.SourceRefName, pr.PullRequest.TargetRefName);

            var iteration = pr.LatestIteration;
            var diffs = await _diffService.GetDiffsAsync(pr, iteration, cancellationToken);

            var reviewResult = await _planner.PlanAsync(pr, iteration, diffs, policy, cancellationToken);

            if (!_options.DryRun)
            {
                await _commentService.ApplyReviewAsync(pr, iteration, reviewResult, cancellationToken);
                await _approvalService.ApplyApprovalAsync(pr, reviewResult, cancellationToken);
            }
            else
            {
                _logger.LogWarning("DRY_RUN enabled: reporting issues without posting to Azure DevOps");
            }

            _logger.LogInformation("AI review completed with {ErrorCount} errors, {WarningCount} warnings", reviewResult.ErrorCount, reviewResult.WarningCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI PR review failed: {Message}", ex.Message);
            Environment.ExitCode = -1;
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
