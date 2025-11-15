using Quaaly.Core.Enums;

namespace Quaaly.Core.Models;

/// <summary>
/// Represents a pull request across different source control providers.
/// </summary>
public sealed class PullRequest
{
    /// <summary>
    /// Unique identifier for the pull request.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Title of the pull request.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Description/body of the pull request.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Current status of the pull request.
    /// </summary>
    public required PullRequestStatus Status { get; init; }

    /// <summary>
    /// Source branch name.
    /// </summary>
    public required string SourceBranch { get; init; }

    /// <summary>
    /// Target branch name.
    /// </summary>
    public required string TargetBranch { get; init; }

    /// <summary>
    /// Author/creator of the pull request.
    /// </summary>
    public required UserIdentity CreatedBy { get; init; }

    /// <summary>
    /// Date when the pull request was created.
    /// </summary>
    public required DateTime CreatedDate { get; init; }

    /// <summary>
    /// Repository containing the pull request.
    /// </summary>
    public required Repository Repository { get; init; }

    /// <summary>
    /// SHA of the last commit in the source branch.
    /// </summary>
    public string? LastSourceCommitId { get; init; }

    /// <summary>
    /// SHA of the last commit in the target branch.
    /// </summary>
    public string? LastTargetCommitId { get; init; }

    /// <summary>
    /// URL to view the pull request in the provider's UI.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Whether the pull request is in draft state.
    /// </summary>
    public bool IsDraft { get; init; }
}
