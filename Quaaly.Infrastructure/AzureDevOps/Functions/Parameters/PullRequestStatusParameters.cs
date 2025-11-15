using System.ComponentModel;

namespace Quaaly.Infrastructure.AzureDevOps.Functions.Parameters;

/// <summary>
/// Parameters for approving or rejecting a pull request.
/// </summary>
public class ApprovePullRequestParameters
{
    /// <summary>
    /// The vote to cast: 10 (approved), 5 (approved with suggestions), 0 (no vote), -5 (waiting for author), -10 (rejected).
    /// </summary>
    [Description("Vote: 10=approved, 5=approved with suggestions, 0=no vote, -5=waiting for author, -10=rejected")]
    public int Vote { get; set; }

    /// <summary>
    /// Optional comment to include with the vote.
    /// </summary>
    [Description("Optional comment to include with the vote")]
    public string? Comment { get; set; }
}

/// <summary>
/// Parameters for completing (merging) a pull request.
/// </summary>
public class CompletePullRequestParameters
{
    /// <summary>
    /// Completion message/comment.
    /// </summary>
    [Description("Completion message/comment")]
    public string? CompletionMessage { get; set; }

    /// <summary>
    /// Whether to delete the source branch after merge.
    /// </summary>
    [Description("Whether to delete the source branch after merge")]
    public bool DeleteSourceBranch { get; set; }

    /// <summary>
    /// Merge strategy: noFastForward, squash, rebase, rebaseMerge.
    /// </summary>
    [Description("Merge strategy: noFastForward, squash, rebase, rebaseMerge")]
    public string? MergeStrategy { get; set; }
}

/// <summary>
/// Parameters for abandoning (closing without merging) a pull request.
/// </summary>
public class AbandonPullRequestParameters
{
    /// <summary>
    /// Reason for abandoning the pull request.
    /// </summary>
    [Description("Reason for abandoning the pull request")]
    public string? Comment { get; set; }
}

/// <summary>
/// Parameters for enabling auto-complete on a pull request.
/// </summary>
public class SetAutoCompleteParameters
{
    /// <summary>
    /// Whether to enable or disable auto-complete.
    /// </summary>
    [Description("Whether to enable or disable auto-complete")]
    public bool Enable { get; set; }

    /// <summary>
    /// Auto-complete message.
    /// </summary>
    [Description("Auto-complete message")]
    public string? Message { get; set; }

    /// <summary>
    /// Whether to delete the source branch after auto-complete.
    /// </summary>
    [Description("Whether to delete the source branch after auto-complete")]
    public bool? DeleteSourceBranch { get; set; }

    /// <summary>
    /// Merge strategy: noFastForward, squash, rebase, rebaseMerge.
    /// </summary>
    [Description("Merge strategy: noFastForward, squash, rebase, rebaseMerge")]
    public string? MergeStrategy { get; set; }
}
