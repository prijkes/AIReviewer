using Quaally.Core.Enums;

namespace Quaally.Core.Models;

/// <summary>
/// Represents a review thread (conversation) on a pull request.
/// </summary>
public sealed class ReviewThread
{
    /// <summary>
    /// Unique identifier for the thread.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Current status of the thread.
    /// </summary>
    public required ThreadStatus Status { get; init; }

    /// <summary>
    /// Comments in the thread.
    /// </summary>
    public required List<Comment> Comments { get; init; }

    /// <summary>
    /// File path if this is an inline comment thread.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Line number where the thread starts (for inline comments).
    /// </summary>
    public int? LineStart { get; init; }

    /// <summary>
    /// Line number where the thread ends (for inline comments).
    /// </summary>
    public int? LineEnd { get; init; }

    /// <summary>
    /// Whether this is a general PR comment (not on specific code).
    /// </summary>
    public bool IsGeneralComment => FilePath == null;

    /// <summary>
    /// Date when the thread was created.
    /// </summary>
    public DateTime? CreatedDate { get; init; }

    /// <summary>
    /// Date when the thread was last updated.
    /// </summary>
    public DateTime? LastUpdatedDate { get; init; }
}
