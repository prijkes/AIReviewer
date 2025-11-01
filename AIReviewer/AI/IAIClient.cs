using AIReviewer.Diff;
using AIReviewer.Review;

namespace AIReviewer.AI;

public interface IAiClient
{
    Task<AiReviewResponse> ReviewAsync(string policy, FileDiff fileDiff, CancellationToken cancellationToken);
    Task<AiReviewResponse> ReviewPullRequestMetadataAsync(string policy, PrMetadata metadata, CancellationToken cancellationToken);
}

public sealed record AiReviewResponse(IReadOnlyList<AiIssue> Issues);

public sealed record AiIssue(
    string Id,
    string Title,
    string Severity,
    string Category,
    string File,
    int Line,
    string Rationale,
    string Recommendation,
    string? FixExample);
