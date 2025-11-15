using Quaally.Core.Models;

namespace Quaally.Core.Interfaces;

/// <summary>
/// Generic interface for interacting with source control providers.
/// Abstracts operations across Azure DevOps, GitHub, GitLab, etc.
/// </summary>
public interface ISourceControlClient
{
    /// <summary>
    /// Gets the pull request information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pull request details.</returns>
    Task<PullRequest> GetPullRequestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all file changes in the pull request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of file changes.</returns>
    Task<List<FileChange>> GetFileChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the content of a file at a specific version.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <param name="version">Version (commit SHA or branch name).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File content as string.</returns>
    Task<string?> GetFileContentAsync(string filePath, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the diff for a specific file in the pull request.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Unified diff text.</returns>
    Task<string?> GetFileDiffAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches the codebase for specific text or patterns.
    /// </summary>
    /// <param name="searchTerm">Text or regex pattern to search for.</param>
    /// <param name="filePattern">Optional file pattern filter (e.g., "*.cs").</param>
    /// <param name="maxResults">Maximum number of results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results as JSON string.</returns>
    Task<string> SearchCodeAsync(string searchTerm, string? filePattern, int maxResults, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the commit history for a specific file.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <param name="maxCommits">Maximum number of commits to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Commit history as JSON string.</returns>
    Task<string> GetFileHistoryAsync(string filePath, int maxCommits, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the authenticated user identity (bot user).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bot's user identity.</returns>
    Task<UserIdentity> GetAuthenticatedUserAsync(CancellationToken cancellationToken = default);
}
