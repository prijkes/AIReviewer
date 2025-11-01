using AIReviewer.Reviewer.AI;
using AIReviewer.Reviewer.AzureDevOps.Models;
using AIReviewer.Reviewer.Diff;
using AIReviewer.Reviewer.Options;
using AIReviewer.Reviewer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIReviewer.Reviewer.Review;

public sealed record PrMetadata(string Title, string Description, IReadOnlyList<string> CommitMessages);

public sealed record ReviewPlanResult(IReadOnlyList<ReviewIssue> Issues, int ErrorCount, int WarningCount, int WarnBudget)
{
    public bool ShouldApprove => ErrorCount == 0 && WarningCount <= WarnBudget;
}

public sealed class ReviewPlanner
{
    private readonly ILogger<ReviewPlanner> _logger;
    private readonly IAiClient _aiClient;
    private readonly ReviewerOptions _options;

    public ReviewPlanner(ILogger<ReviewPlanner> logger, IAiClient aiClient, IOptionsMonitor<ReviewerOptions> options)
    {
        _logger = logger;
        _aiClient = aiClient;
        _options = options.CurrentValue;
    }

    public async Task<ReviewPlanResult> PlanAsync(PullRequestContext pr, Microsoft.TeamFoundation.SourceControl.WebApi.GitPullRequestIteration iteration, IReadOnlyList<FileDiff> diffs, string policy, CancellationToken cancellationToken)
    {
        var issues = new List<ReviewIssue>();

        foreach (var diff in diffs.Take(50))
        {
            if (diff.DiffText.Length > _options.MaxDiffBytes)
            {
                _logger.LogWarning("Skipping large diff {Path} ({Size} bytes)", diff.Path, diff.DiffText.Length);
                continue;
            }

            var aiResponse = await _aiClient.ReviewAsync(policy, diff, cancellationToken);
            foreach (var issue in aiResponse.Issues.Take(5))
            {
                var fingerprint = ComputeFingerprint(diff, iteration.Id, issue);
                issues.Add(new ReviewIssue
                {
                    Id = issue.Id,
                    Title = issue.Title,
                    Severity = ParseSeverity(issue.Severity),
                    Category = ParseCategory(issue.Category),
                    FilePath = string.IsNullOrEmpty(issue.File) ? diff.Path : issue.File,
                    Line = issue.Line,
                    Rationale = issue.Rationale,
                    Recommendation = issue.Recommendation,
                    FixExample = issue.FixExample,
                    Fingerprint = fingerprint
                });
            }
        }

        var metadataIssues = await ReviewMetadataAsync(pr, policy, iteration.Id, cancellationToken);
        issues.AddRange(metadataIssues);

        var errorCount = issues.Count(i => i.Severity == IssueSeverity.Error);
        var warningCount = issues.Count(i => i.Severity == IssueSeverity.Warn);

        return new ReviewPlanResult(issues, errorCount, warningCount, _options.WarnBudget);
    }

    private async Task<IEnumerable<ReviewIssue>> ReviewMetadataAsync(PullRequestContext pr, string policy, int iterationId, CancellationToken cancellationToken)
    {
        var metadata = new PrMetadata(
            pr.PullRequest.Title ?? string.Empty,
            pr.PullRequest.Description ?? string.Empty,
            pr.Commits.Select(c => c.Comment ?? string.Empty).ToList());

        var response = await _aiClient.ReviewPullRequestMetadataAsync(policy, metadata, cancellationToken);
        return response.Issues.Select(issue =>
        {
            var fingerprint = Logging.HashSha256($"{issue.Id}:{issue.Title}:{iterationId}");
            return new ReviewIssue
            {
                Id = issue.Id,
                Title = issue.Title,
                Severity = ParseSeverity(issue.Severity),
                Category = ParseCategory(issue.Category),
                FilePath = string.Empty,
                Line = 0,
                Rationale = issue.Rationale,
                Recommendation = issue.Recommendation,
                FixExample = issue.FixExample,
                Fingerprint = fingerprint
            };
        });
    }

    private static IssueCategory ParseCategory(string category) => category.ToLowerInvariant() switch
    {
        "security" => IssueCategory.Security,
        "correctness" => IssueCategory.Correctness,
        "performance" => IssueCategory.Performance,
        "docs" => IssueCategory.Docs,
        "tests" => IssueCategory.Tests,
        _ => IssueCategory.Style
    };

    private static IssueSeverity ParseSeverity(string severity) => severity.ToLowerInvariant() switch
    {
        "error" => IssueSeverity.Error,
        "warn" => IssueSeverity.Warn,
        _ => IssueSeverity.Info
    };

    private static string ComputeFingerprint(FileDiff diff, int iterationId, AiIssue issue) =>
        Logging.HashSha256($"{diff.Path}:{issue.Line}:{issue.Id}:{issue.Title}:{iterationId}:{diff.FileHash}");
}
