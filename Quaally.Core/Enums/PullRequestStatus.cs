namespace Quaally.Core.Enums;

/// <summary>
/// Generic pull request status across all providers.
/// </summary>
public enum PullRequestStatus
{
    /// <summary>
    /// Pull request is active and open.
    /// </summary>
    Active,

    /// <summary>
    /// Pull request has been completed (merged).
    /// </summary>
    Completed,

    /// <summary>
    /// Pull request has been abandoned or closed without merging.
    /// </summary>
    Abandoned,

    /// <summary>
    /// Pull request is in draft state.
    /// </summary>
    Draft
}
