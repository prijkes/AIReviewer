using Quaaly.Infrastructure.Diff;
using Quaaly.Infrastructure.Review;
using Quaaly.Infrastructure.AzureDevOps.Models;
using Quaaly.Infrastructure.Utils;

namespace Quaaly.Infrastructure.AI;

/// <summary>
/// Interface for AI clients that perform code reviews using large language models.
/// </summary>
public interface IAiClient
{
    /// <summary>
    /// Reviews a file diff against the provided policy and returns identified issues.
    /// </summary>
    /// <param name="policy">The review policy containing guidelines and rules.</param>
    /// <param name="fileDiff">The file diff to review.</param>
    /// <param name="language">The language code for the review response ("en" for English, "ja" for Japanese).</param>
    /// <param name="programmingLanguage">The programming language of the file being reviewed.</param>
    /// <param name="existingComments">Existing comments on this file from other reviewers to avoid duplicates.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A response containing the list of issues found.</returns>
    Task<AiReviewResponse> ReviewAsync(string policy, ReviewFileDiff fileDiff, string language, ProgrammingLanguageDetector.ProgrammingLanguage programmingLanguage, List<ExistingComment> existingComments, CancellationToken cancellationToken);

    /// <summary>
    /// Reviews pull request metadata (title, description, commits) for hygiene and completeness.
    /// </summary>
    /// <param name="policy">The review policy containing metadata review guidelines.</param>
    /// <param name="metadata">The PR metadata to review.</param>
    /// <param name="language">The language code for the review response ("en" for English, "ja" for Japanese).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A response containing the list of issues found.</returns>
    Task<AiReviewResponse> ReviewPullRequestMetadataAsync(string policy, PullRequestMetadata metadata, string language, CancellationToken cancellationToken);
}

/// <summary>
/// Represents the response from an AI review containing identified issues.
/// </summary>
/// <param name="Issues">The list of issues identified by the AI.</param>
public sealed record AiReviewResponse(IReadOnlyList<AiIssue> Issues);

/// <summary>
/// Represents a single issue identified during AI code review.
/// </summary>
/// <param name="Title">Brief title describing the issue.</param>
/// <param name="Severity">Severity level of the issue.</param>
/// <param name="Category">Category classification of the issue.</param>
/// <param name="File">File path where the issue was found.</param>
/// <param name="FileLineStart">Starting line number in the actual file where the issue begins.</param>
/// <param name="FileLineStartOffset">Character offset within the starting line where the issue begins (0-based).</param>
/// <param name="FileLineEnd">Ending line number in the actual file where the issue ends.</param>
/// <param name="FileLineEndOffset">Character offset within the ending line where the issue ends (0-based).</param>
/// <param name="Rationale">Explanation of why this is an issue.</param>
/// <param name="Recommendation">Suggested fix or improvement.</param>
/// <param name="FixExample">Optional code example showing how to fix the issue.</param>
public sealed record AiIssue(
    string Title,
    IssueSeverity Severity,
    IssueCategory Category,
    string File,
    int FileLineStart,
    int FileLineStartOffset,
    int FileLineEnd,
    int FileLineEndOffset,
    string Rationale,
    string Recommendation,
    string? FixExample);
