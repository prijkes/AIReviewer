namespace Quaally.Core.Models;

/// <summary>
/// Represents a comment event from any source control provider.
/// </summary>
public sealed class CommentEvent
{
    /// <summary>
    /// The pull request where the comment was made.
    /// </summary>
    public required PullRequest PullRequest { get; init; }

    /// <summary>
    /// The comment that was created or updated.
    /// </summary>
    public required Comment Comment { get; init; }

    /// <summary>
    /// Type of event (commented, edited, deleted, etc.).
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    public required DateTime Timestamp { get; init; }
}
