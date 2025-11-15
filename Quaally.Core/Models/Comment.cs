namespace Quaally.Core.Models;

/// <summary>
/// Represents a comment on a pull request.
/// </summary>
public sealed class Comment
{
    /// <summary>
    /// Unique identifier for the comment.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Content/text of the comment.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Author of the comment.
    /// </summary>
    public required UserIdentity Author { get; init; }

    /// <summary>
    /// Date when the comment was published.
    /// </summary>
    public required DateTime PublishedDate { get; init; }

    /// <summary>
    /// Parent comment ID if this is a reply.
    /// </summary>
    public int? ParentCommentId { get; init; }

    /// <summary>
    /// Whether this comment was edited.
    /// </summary>
    public bool IsEdited { get; init; }

    /// <summary>
    /// Last updated date if edited.
    /// </summary>
    public DateTime? LastUpdatedDate { get; init; }
}
