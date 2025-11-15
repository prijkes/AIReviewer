using Quaaly.Infrastructure.AI;
using Quaaly.Infrastructure.AzureDevOps;
using Quaaly.Infrastructure.AzureDevOps.Models;
using Quaaly.Infrastructure.Diff;
using Quaaly.Infrastructure.Options;
using Quaaly.Infrastructure.Policy;
using Quaaly.Infrastructure.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace Quaaly.Infrastructure.Review;

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
/// <param name="Iteration">The PR iteration number.</param>
/// <param name="Issues">All identified issues from the review.</param>
/// <param name="ErrorCount">Number of error-level issues.</param>
/// <param name="WarningCount">Number of warning-level issues.</param>
/// <param name="WarnBudget">Maximum allowed warnings before rejecting approval.</param>
public sealed record ReviewPlanResult(int Iteration, IReadOnlyList<ReviewIssue> Issues, int ErrorCount, int WarningCount, int WarnBudget)
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
/// <param name="adoClient">ADO client for retrieving existing PR comments.</param>
/// <param name="contextRetriever">Context retriever for function calling support.</param>
/// <param name="policyLoader">Policy loader for loading language-specific policies.</param>
/// <param name="options">Configuration options for the reviewer.</param>
public sealed class ReviewPlanner(ILogger<ReviewPlanner> logger, IAiClient aiClient, IAdoSdkClient adoClient, ReviewContextRetriever contextRetriever, PolicyLoader policyLoader, IOptionsMonitor<ReviewerOptions> options)
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
    /// <param name="threads">The threads to analyze.</param>
    /// <param name="basePolicyPath">The base policy file path.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A <see cref="ReviewPlanResult"/> containing all identified issues and counts.</returns>
    public async Task<ReviewPlanResult> PlanAsync(PullRequestContext pr, GitPullRequestIteration iteration, IReadOnlyList<ReviewFileDiff> diffs, List<GitPullRequestCommentThread> threads, string basePolicyPath, CancellationToken cancellationToken)
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

        var existingCommentsByFile = adoClient.GetExistingCommentsFromThreads(threads);

        logger.LogInformation("Pre-fetched {ThreadCount} threads with comments for {FileCount} files to optimize parallel reviews",
            threads.Count, existingCommentsByFile.Count);

        // Review files in parallel with pre-fetched comments
        var reviewTasks = diffsToReview.Select(diff =>
            ReviewFileAsync(diff, basePolicyPath, language,
                existingCommentsByFile.GetValueOrDefault(diff.Path, []),
                cancellationToken));

        var fileResults = await Task.WhenAll(reviewTasks);

        // Flatten all issues
        var issues = fileResults.SelectMany(r => r).ToList();

        // Review metadata (use general policy for metadata)
        var generalPolicy = await policyLoader.LoadAsync(basePolicyPath, cancellationToken);
        var metadataIssues = await ReviewMetadataAsync(pr, generalPolicy, language, iteration.Id, cancellationToken);
        issues.AddRange(metadataIssues);

        var errorCount = issues.Count(i => i.Severity == IssueSeverity.Error);
        var warningCount = issues.Count(i => i.Severity == IssueSeverity.Warn);

        return new ReviewPlanResult(iteration.Id ?? 0, issues, errorCount, warningCount, _options.WarnBudget);
    }

    /// <summary>
    /// Reviews a single file and returns the identified issues.
    /// Detects the programming language and loads the appropriate policy.
    /// Uses pre-fetched existing comments to provide context and avoid duplicates.
    /// </summary>
    private async Task<List<ReviewIssue>> ReviewFileAsync(
        ReviewFileDiff diff,
        string basePolicyPath,
        string language,
        List<ExistingComment> existingComments,
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

            logger.LogDebug("Using {CommentCount} pre-fetched existing comments for {FilePath}", existingComments.Count, diff.Path);

            var aiResponse = await aiClient.ReviewAsync(policy, diff, language, programmingLanguage, existingComments, cancellationToken);

            if (aiResponse.Issues.Count > _options.MaxIssuesPerFile)
            {
                logger.LogWarning("File {Path} has {TotalIssues} issues, reporting first {MaxIssues} only",
                    diff.Path, aiResponse.Issues.Count, _options.MaxIssuesPerFile);
            }

            foreach (var issue in aiResponse.Issues.Take(_options.MaxIssuesPerFile))
            {
                var fingerprint = ComputeFingerprint(diff);
                logger.LogInformation(
                    "[FINGERPRINT] Generated fingerprint for {FilePath} = {Fingerprint} (FileHash={FileHash})",
                    diff.Path, fingerprint, diff.FileHash);

                issues.Add(new ReviewIssue
                {
                    Title = issue.Title,
                    Severity = issue.Severity,
                    Category = issue.Category,
                    FilePath = string.IsNullOrEmpty(issue.File) ? diff.Path : issue.File,
                    LineStart = issue.FileLineStart,
                    LineStartOffset = issue.FileLineStartOffset,
                    LineEnd = issue.FileLineEnd,
                    LineEndOffset = issue.FileLineEndOffset,
                    Rationale = issue.Rationale,
                    Recommendation = issue.Recommendation,
                    FixExample = issue.FixExample,
                    Fingerprint = fingerprint,
                    IsDeleted = diff.IsDeleted
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
        
        // Use PR description content for fingerprint - ensures deterministic fingerprinting
        // that only changes when the description itself changes, avoiding duplicate comments
        var descriptionContent = pr.PullRequest.Description ?? string.Empty;
        var baseFingerprint = Logging.HashSha256(descriptionContent);
        
        return response.Issues.Select(issue =>
        {
            logger.LogInformation(
                "[FINGERPRINT-METADATA] Generated fingerprint for metadata issue = {Fingerprint} (based on description content hash)",
                baseFingerprint);

            return new ReviewIssue
            {
                Title = issue.Title,
                Severity = issue.Severity,
                Category = issue.Category,
                FilePath = string.Empty,
                LineStart = 0,
                LineEnd = 0,
                Rationale = issue.Rationale,
                Recommendation = issue.Recommendation,
                FixExample = issue.FixExample,
                Fingerprint = baseFingerprint
            };
        });
    }

    /// <summary>
    /// Computes a unique fingerprint for an issue based on stable identifiers only.
    /// Uses file path, and file hash to ensure consistent
    /// fingerprints across multiple bot runs, preventing duplicate comments.
    /// </summary>
    /// <param name="diff">The file diff containing the issue.</param>
    /// <param name="iterationId">The PR iteration ID.</param>
    /// <returns>A SHA-256 hash fingerprint string.</returns>
    private static string ComputeFingerprint(ReviewFileDiff diff) =>
        Logging.HashSha256($"{diff.Path}:{diff.FileHash}");
}
