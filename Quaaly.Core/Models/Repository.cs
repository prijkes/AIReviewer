namespace Quaaly.Core.Models;

/// <summary>
/// Represents a source control repository.
/// </summary>
public sealed class Repository
{
    /// <summary>
    /// Unique identifier for the repository.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Name of the repository.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Default branch name (e.g., "main", "master").
    /// </summary>
    public string? DefaultBranch { get; init; }

    /// <summary>
    /// Remote URL of the repository.
    /// </summary>
    public string? RemoteUrl { get; init; }

    /// <summary>
    /// Project or organization name.
    /// </summary>
    public string? Project { get; init; }
}
