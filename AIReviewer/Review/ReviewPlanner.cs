using AIReviewer.AI;
using AIReviewer.AzureDevOps.Models;
using AIReviewer.Diff;
using AIReviewer.Options;
using AIReviewer.Policy;
using AIReviewer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AIReviewer.Review;

/// <summary>
/// Contains pull request metadata for hygiene review.
/// </summary>
/// <param name="Title">The PR title.</param>
/// <param name="Description">The PR description.</param>
/// <param name="CommitMessages">List of commit messages in the PR.</param>
public sealed record PullRequestMetadata(string Title, string Description, IReadOnlyList<string> CommitMessages);

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
/// <remarks>
/// Initializes a new instance of the <see cref="ReviewPlanner"/> class.
/// </remarks>
/// <param name="logger">Logger for diagnostic information.</param>
/// <param name="aiClient">AI client for performing code reviews.</param>
/// <param name="contextRetriever">Context retriever for function calling support.</param>
/// <param name="policyLoader">Policy loader for loading language-specific policies.</param>
/// <param name="options">Configuration options for the reviewer.</param>
public sealed class ReviewPlanner(ILogger<ReviewPlanner> logger, IAiClient aiClient, ReviewContextRetriever contextRetriever, PolicyLoader policyLoader, IOptionsMonitor<ReviewerOptions> options)
{
    private readonly ReviewerOptions _options = options.CurrentValue;

    /// <summary>
    /// Plans and executes the complete review process for a pull request.
    /// Reviews all file diffs and PR metadata, collecting issues from the AI.
    /// Files are reviewed in parallel for improved performance.
    /// Language-specific prompts and policies are applied based on file type.
    /// </summary>
    /// <param name="pr">The pull request context.</param>
    /// <param name="iteration">The current PR iteration.</param>
    /// <param name="diffs">The list of file diffs to review.</param>
    /// <param name="basePolicyPath">The base policy file path.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A <see cref="ReviewPlanResult"/> containing all identified issues and counts.</returns>
    public async Task<ReviewPlanResult> PlanAsync(PullRequestContext pr, GitPullRequestIteration iteration, IReadOnlyList<ReviewFileDiff> diffs, string basePolicyPath, CancellationToken cancellationToken)
    {
        // Set PR context for function calling (if available)
        contextRetriever?.SetContext(pr);

        // Detect language from PR description
        var prDescription = pr.PullRequest.Description ?? string.Empty;
        var language = LanguageDetector.DetectLanguage(prDescription, _options.JapaneseDetectionThreshold);
        logger.LogInformation("Detected review language: {Language} (based on PR description, threshold: {Threshold})", 
            language, _options.JapaneseDetectionThreshold);

        if (diffs.Count > _options.MaxFilesToReview)
        {
            logger.LogWarning("PR has {TotalFiles} files, reviewing first {MaxFiles} only", diffs.Count, _options.MaxFilesToReview);
        }

        // Filter and prepare diffs for review
        var diffsToReview = diffs
            .Take(_options.MaxFilesToReview)
            .Where(diff =>
            {
                if (diff.DiffText.Length > _options.MaxDiffBytes)
                {
                    logger.LogWarning("Skipping large diff {Path} ({Size} bytes)", diff.Path, diff.DiffText.Length);
                    return false;
                }
                return true;
            })
            .ToList();

        // Review files in parallel
        var reviewTasks = diffsToReview.Select(diff => 
            ReviewFileAsync(diff, basePolicyPath, language, iteration.Id ?? 0, cancellationToken));

        var fileResults = await Task.WhenAll(reviewTasks);

        // Flatten all issues
        var issues = fileResults.SelectMany(r => r).ToList();

        // Review metadata (use general policy for metadata)
        var generalPolicy = await policyLoader.LoadAsync(basePolicyPath, cancellationToken);
        var metadataIssues = await ReviewMetadataAsync(pr, generalPolicy, language, iteration.Id, cancellationToken);
        issues.AddRange(metadataIssues);

        var errorCount = issues.Count(i => i.Severity == IssueSeverity.Error);
        var warningCount = issues.Count(i => i.Severity == IssueSeverity.Warn);

        return new ReviewPlanResult(issues, errorCount, warningCount, _options.WarnBudget);
    }

    /// <summary>
    /// Reviews a single file and returns the identified issues.
    /// Detects the programming language and loads the appropriate policy.
    /// </summary>
    private async Task<List<ReviewIssue>> ReviewFileAsync(
        ReviewFileDiff diff, 
        string basePolicyPath, 
        string language, 
        int iterationId, 
        CancellationToken cancellationToken)
    {
        var issues = new List<ReviewIssue>();

        try
        {
            // Detect programming language for the file
            var programmingLanguage = ProgrammingLanguageDetector.DetectLanguage(diff.Path);
            
            // Log the detected programming language
            logger.LogDebug("File {Path} detected as {Language}", 
                diff.Path, 
                ProgrammingLanguageDetector.GetDisplayName(programmingLanguage));

            // Load language-specific policy
            var policy = await policyLoader.LoadLanguageSpecificAsync(basePolicyPath, programmingLanguage, cancellationToken);

            var aiResponse = await aiClient.ReviewAsync(policy, diff, language, programmingLanguage, cancellationToken);
            
            if (aiResponse.Issues.Count > _options.MaxIssuesPerFile)
            {
                logger.LogWarning("File {Path} has {TotalIssues} issues, reporting first {MaxIssues} only", 
                    diff.Path, aiResponse.Issues.Count, _options.MaxIssuesPerFile);
            }

            foreach (var issue in aiResponse.Issues.Take(_options.MaxIssuesPerFile))
            {
                var fingerprint = ComputeFingerprint(diff, iterationId, issue);
                issues.Add(new ReviewIssue
                {
                    Id = issue.Id,
                    Title = issue.Title,
                    Severity = issue.Severity,
                    Category = issue.Category,
                    FilePath = string.IsNullOrEmpty(issue.File) ? diff.Path : issue.File,
                    Line = issue.Line,
                    Rationale = issue.Rationale,
                    Recommendation = issue.Recommendation,
                    FixExample = issue.FixExample,
                    Fingerprint = fingerprint
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to review file {Path}, continuing with other files", diff.Path);
        }

        return issues;
    }

    /// <summary>
    /// Reviews the pull request metadata (title, description, commits) for hygiene and completeness.
    /// </summary>
    /// <param name="pr">The pull request context.</param>
    /// <param name="policy">The review policy to apply.</param>
    /// <param name="language">The language code for the review response.</param>
    /// <param name="iterationId">The current iteration ID for fingerprinting.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A collection of issues found in the PR metadata.</returns>
    private async Task<IEnumerable<ReviewIssue>> ReviewMetadataAsync(PullRequestContext pr, string policy, string language, int? iterationId, CancellationToken cancellationToken)
    {
        var metadata = new PullRequestMetadata(
            pr.PullRequest.Title ?? string.Empty,
            pr.PullRequest.Description ?? string.Empty,
            [.. pr.Commits.Select(c => c.Comment ?? string.Empty)]);

        var response = await aiClient.ReviewPullRequestMetadataAsync(policy, metadata, language, cancellationToken);
        return response.Issues.Select(issue =>
        {
            var fingerprint = Logging.HashSha256($"{issue.Id}:{issue.Title}:{iterationId ?? 0}");
            return new ReviewIssue
            {
                Id = issue.Id,
                Title = issue.Title,
                Severity = issue.Severity,
                Category = issue.Category,
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
    /// Computes a unique fingerprint for an issue based on file path, line, ID, and other attributes.
    /// This fingerprint is used to track issues across PR iterations.
    /// </summary>
    /// <param name="diff">The file diff containing the issue.</param>
    /// <param name="iterationId">The PR iteration ID.</param>
    /// <param name="issue">The AI-identified issue.</param>
    /// <returns>A SHA-256 hash fingerprint string.</returns>
    private static string ComputeFingerprint(ReviewFileDiff diff, int iterationId, AiIssue issue) =>
        Logging.HashSha256($"{diff.Path}:{issue.Line}:{issue.Id}:{issue.Title}:{iterationId}:{diff.FileHash}");
}
