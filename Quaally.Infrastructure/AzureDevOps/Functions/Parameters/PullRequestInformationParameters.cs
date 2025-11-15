using System.ComponentModel;

namespace Quaally.Infrastructure.AzureDevOps.Functions.Parameters;

/// <summary>
/// Parameters for getting files in a pull request.
/// </summary>
public class GetPullRequestFilesParameters
{
    /// <summary>
    /// Maximum number of files to return (optional, defaults to all).
    /// </summary>
    [Description("Maximum number of files to return")]
    public int? MaxFiles { get; set; }
}

/// <summary>
/// Parameters for getting a diff for a specific file in the pull request.
/// </summary>
public class GetPullRequestDiffParameters
{
    /// <summary>
    /// The file path to get the diff for.
    /// </summary>
    [Description("The file path to get the diff for")]
    public string FilePath { get; set; } = string.Empty;
}

/// <summary>
/// Parameters for getting details about a specific commit.
/// </summary>
public class GetCommitDetailsParameters
{
    /// <summary>
    /// The commit ID (SHA) to retrieve details for.
    /// </summary>
    [Description("The commit ID (SHA) to retrieve details for")]
    public string CommitId { get; set; } = string.Empty;
}

/// <summary>
/// Parameters for getting all commits in a pull request.
/// </summary>
public class GetPullRequestCommitsParameters
{
    /// <summary>
    /// Maximum number of commits to return (optional).
    /// </summary>
    [Description("Maximum number of commits to return")]
    public int? MaxCommits { get; set; }
}

/// <summary>
/// Parameters for getting work items linked to a pull request.
/// </summary>
public class GetPullRequestWorkItemsParameters
{
    // No additional parameters needed - uses current PR context
}
