namespace Quaaly.Infrastructure.AzureDevOps.Models;

/// <summary>
/// Represents an existing comment on a pull request file.
/// Used to provide context to AI to avoid duplicate feedback.
/// </summary>
/// <param name="Author">The name of the comment author.</param>
/// <param name="Content">The comment text content.</param>
/// <param name="FilePath">The file path this comment is on.</param>
/// <param name="LineNumber">The line number (if applicable).</param>
/// <param name="ThreadStatus">The status of the comment thread.</param>
public sealed record ExistingComment(
    string Author,
    string Content,
    string FilePath,
    int? LineNumber,
    string ThreadStatus);
