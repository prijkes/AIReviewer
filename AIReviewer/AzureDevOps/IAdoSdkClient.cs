using AIReviewer.AzureDevOps.Models;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace AIReviewer.AzureDevOps;

/// <summary>
/// Interface for interacting with Azure DevOps.
/// Provides methods to fetch pull request context, repositories, and iteration changes.
/// </summary>
public interface IAdoSdkClient
{
    /// <summary>
    /// Retrieves the complete pull request context including PR details, repository, commits, and iterations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A <see cref="PullRequestContext"/> containing all PR-related information.</returns>
    Task<PullRequestContext> GetPullRequestContextAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the list of file changes for a specific PR iteration.
    /// </summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="prId">The pull request ID.</param>
    /// <param name="iterationId">The iteration ID.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The iteration changes containing modified files.</returns>
    Task<GitPullRequestIterationChanges> GetIterationChangesAsync(Guid repoId, int prId, int iterationId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the underlying Git HTTP client for direct ADO API access.
    /// </summary>
    GitHttpClient Git { get; }

    /// <summary>
    /// Retrieves the authorized identity (bot user) for the current connection.
    /// </summary>
    /// <returns>An <see cref="IdentityRef"/> representing the bot identity.</returns>
    IdentityRef GetAuthorizedIdentity();

    /// <summary>
    /// Retrieves the content of a file at a specific version (commit or branch) using local git repository.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="versionDescriptor">The version (commit SHA or branch ref).</param>
    /// <returns>The file item with content.</returns>
    Task<GitItem?> GetFileContentAsync(string filePath, string versionDescriptor);

    /// <summary>
    /// Searches the codebase for files containing specific text using local git repository.
    /// </summary>
    /// <param name="searchTerm">The text to search for.</param>
    /// <param name="versionDescriptor">The version (branch ref) to search in.</param>
    /// <param name="filePattern">Optional file pattern filter (e.g., "*.cs").</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <returns>List of search results.</returns>
    Task<List<CodeSearchResult>> SearchCodeAsync(string searchTerm, string versionDescriptor, string? filePattern, int maxResults);

    /// <summary>
    /// Gets the commit history for a specific file using local git repository.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="versionDescriptor">The version (branch ref) to get history from.</param>
    /// <param name="maxCommits">Maximum number of commits to return.</param>
    /// <returns>List of commits that modified the file.</returns>
    Task<List<GitCommitRef>> GetFileHistoryAsync(string filePath, string versionDescriptor, int maxCommits);

    /// <summary>
    /// Gets the unified diff for a file between two commits using local git repository.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="baseCommit">The base commit SHA.</param>
    /// <param name="targetCommit">The target commit SHA.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The unified diff text.</returns>
    Task<string?> GetFileDiffAsync(string filePath, string baseCommit, string targetCommit, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all existing comments on a pull request for a specific file.
    /// Used to provide context to AI to avoid duplicate feedback.
    /// </summary>
    /// <param name="repoId">The repository ID.</param>
    /// <param name="prId">The pull request ID.</param>
    /// <param name="filePath">The file path to get comments for (null for all files).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>List of existing comments on the file.</returns>
    Task<List<ExistingComment>> GetExistingCommentsAsync(Guid repoId, int prId, string? filePath, CancellationToken cancellationToken);
}
