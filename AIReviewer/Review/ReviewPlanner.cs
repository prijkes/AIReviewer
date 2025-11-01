using AIReviewer.AI;
using AIReviewer.AzureDevOps.Models;
using AIReviewer.Diff;
using AIReviewer.Options;
using AIReviewer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIReviewer.Review;

/// <summary>
/// Contains pull request metadata for hygiene review.
/// </summary>
/// <param name="Title">The PR title.</param>
/// <param name="Description">The PR description.</param>
/// <param name="CommitMessages">List of commit messages in the PR.</param>
public sealed record PrMetadata(string Title, string Description, IReadOnlyList<string> CommitMessages);

/// <summary>
/// Contains the results of a code review plan including all identified issues and counts.
/// </summary>
/// <param name="Issues">All identified issues from the review.</param>
/// <param name="ErrorCount">Number of error-level issues.</param>
/// <param name="WarningCount">Number of warning-level issues.</param>
/// <param name="WarnBudget">Maximum allowed warnings before rejecting approval.</param>
public sealed record ReviewPlanResult(IReadOnlyList<ReviewIssue> Issues, int ErrorCount, int WarningCount, int WarnBudget)
{
    /// <summary>
    /// Indicates whether the PR should be approved based on error and warning counts.
    /// </summary>
    public bool ShouldApprove => ErrorCount == 0 && WarningCount <= WarnBudget;
}

/// <summary>
/// Orchestrates the AI review process by analyzing file diffs and PR metadata to produce review results.
/// </summary>
public sealed class ReviewPlanner
{
    private readonly ILogger<ReviewPlanner> _logger;
    private readonly IAiClient _aiClient;
    private readonly ReviewerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewPlanner"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="aiClient">AI client for performing code reviews.</param>
    /// <param name="options">Configuration options for the reviewer.</param>
    public ReviewPlanner(ILogger<ReviewPlanner> logger, IAiClient aiClient, IOptionsMonitor<ReviewerOptions> options)
    {
        _logger = logger;
        _aiClient = aiClient;
        _options = options.CurrentValue;
    }

    /// <summary>
    /// Plans and executes the complete review process for a pull request.
    /// Reviews all file diffs and PR metadata, collecting issues from the AI.
    /// </summary>
    /// <param name="pr">The pull request context.</param>
    /// <param name="iteration">The current PR iteration.</param>
    /// <param name="diffs">The list of file diffs to review.</param>
    /// <param name="policy">The review policy to apply.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A <see cref="ReviewPlanResult"/> containing all identified issues and counts.</returns>
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

    /// <summary>
    /// Reviews the pull request metadata (title, description, commits) for hygiene and completeness.
    /// </summary>
    /// <param name="pr">The pull request context.</param>
    /// <param name="policy">The review policy to apply.</param>
    /// <param name="iterationId">The current iteration ID for fingerprinting.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A collection of issues found in the PR metadata.</returns>
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

    /// <summary>
    /// Parses a category string from the AI response to an enum value.
    /// </summary>
    /// <param name="category">The category string to parse.</param>
    /// <returns>The corresponding <see cref="IssueCategory"/> enum value.</returns>
    private static IssueCategory ParseCategory(string category) => category.ToLowerInvariant() switch
    {
        "security" => IssueCategory.Security,
        "correctness" => IssueCategory.Correctness,
        "performance" => IssueCategory.Performance,
        "docs" => IssueCategory.Docs,
        "tests" => IssueCategory.Tests,
        _ => IssueCategory.Style
    };

    /// <summary>
    /// Parses a severity string from the AI response to an enum value.
    /// </summary>
    /// <param name="severity">The severity string to parse.</param>
    /// <returns>The corresponding <see cref="IssueSeverity"/> enum value.</returns>
    private static IssueSeverity ParseSeverity(string severity) => severity.ToLowerInvariant() switch
    {
        "error" => IssueSeverity.Error,
        "warn" => IssueSeverity.Warn,
        _ => IssueSeverity.Info
    };

    /// <summary>
    /// Computes a unique fingerprint for an issue based on file path, line, ID, and other attributes.
    /// This fingerprint is used to track issues across PR iterations.
    /// </summary>
    /// <param name="diff">The file diff containing the issue.</param>
    /// <param name="iterationId">The PR iteration ID.</param>
    /// <param name="issue">The AI-identified issue.</param>
    /// <returns>A SHA-256 hash fingerprint string.</returns>
    private static string ComputeFingerprint(FileDiff diff, int iterationId, AiIssue issue) =>
        Logging.HashSha256($"{diff.Path}:{issue.Line}:{issue.Id}:{issue.Title}:{iterationId}:{diff.FileHash}");
}
