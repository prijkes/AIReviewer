using AIReviewer.Diff;
using AIReviewer.Review;
using AIReviewer.AzureDevOps.Models;

namespace AIReviewer.AI;

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
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A response containing the list of issues found.</returns>
    Task<AiReviewResponse> ReviewAsync(string policy, ReviewFileDiff fileDiff, string language, CancellationToken cancellationToken);

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
/// <param name="Id">Unique identifier for the issue.</param>
/// <param name="Title">Brief title describing the issue.</param>
/// <param name="Severity">Severity level of the issue.</param>
/// <param name="Category">Category classification of the issue.</param>
/// <param name="File">File path where the issue was found.</param>
/// <param name="Line">Line number where the issue occurs.</param>
/// <param name="Rationale">Explanation of why this is an issue.</param>
/// <param name="Recommendation">Suggested fix or improvement.</param>
/// <param name="FixExample">Optional code example showing how to fix the issue.</param>
public sealed record AiIssue(
    string Id,
    string Title,
    IssueSeverity Severity,
    IssueCategory Category,
    string File,
    int Line,
    string Rationale,
    string Recommendation,
    string? FixExample);
