using AIReviewer.Diff;
using AIReviewer.Review;

namespace AIReviewer.AI.Providers;

/// <summary>
/// Abstraction for AI provider implementations.
/// Allows support for multiple AI services (Azure OpenAI, OpenAI, Anthropic, etc.).
/// </summary>
public interface IAiProvider
{
    /// <summary>
    /// Gets the name of this provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Reviews a file diff using the AI provider.
    /// </summary>
    Task<AiReviewResponse> ReviewAsync(string policy, ReviewFileDiff fileDiff, string language, CancellationToken cancellationToken);

    /// <summary>
    /// Reviews pull request metadata using the AI provider.
    /// </summary>
    Task<AiReviewResponse> ReviewPullRequestMetadataAsync(string policy, PullRequestMetadata metadata, string language, CancellationToken cancellationToken);

    /// <summary>
    /// Gets token usage for the last request.
    /// </summary>
    (long InputTokens, long OutputTokens) GetLastTokenUsage();
}
