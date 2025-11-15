using Quaally.Core.Enums;

namespace Quaally.Core.Models;

/// <summary>
/// Represents a file change in a pull request.
/// </summary>
public sealed class FileChange
{
    /// <summary>
    /// Path to the file.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Type of change made to the file.
    /// </summary>
    public required FileChangeType ChangeType { get; init; }

    /// <summary>
    /// Original path (for renamed files).
    /// </summary>
    public string? OriginalPath { get; init; }

    /// <summary>
    /// Number of additions in the file.
    /// </summary>
    public int? Additions { get; init; }

    /// <summary>
    /// Number of deletions in the file.
    /// </summary>
    public int? Deletions { get; init; }
}
