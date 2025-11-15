using Quaally.Infrastructure.AzureDevOps;
using Quaally.Infrastructure.AzureDevOps.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Quaally.Infrastructure.AI;

/// <summary>
/// Provides context retrieval functions that can be called by the AI during code review.
/// These functions allow the AI to gather additional information about the codebase.
/// </summary>
public partial class ReviewContextRetriever(ILogger<ReviewContextRetriever> logger, IAdoSdkClient adoClient)
{
    private PullRequestContext? _currentPrContext;

    /// <summary>
    /// Sets the current pull request context for function calls.
    /// Must be called before any context retrieval functions.
    /// </summary>
    public void SetContext(PullRequestContext prContext)
    {
        _currentPrContext = prContext;
    }

    /// <summary>
    /// Gets the current pull request context.
    /// </summary>
    /// <returns>The current PR context, or null if not set.</returns>
    public PullRequestContext? GetPullRequestContext()
    {
        return _currentPrContext;
    }

    /// <summary>
    /// Gets the full content of a file from the target branch (where PR is merging to).
    /// Useful for understanding the complete context of changes.
    /// </summary>
    /// <param name="filePath">The path to the file (e.g., "src/Program.cs")</param>
    /// <returns>The full file content as a string</returns>
    public async Task<string> GetFullFileContentAsync(string filePath)
    {
        EnsureContextSet();

        try
        {
            logger.LogDebug("AI requested full file content: {FilePath}", filePath);

            var item = await adoClient.GetFileContentAsync(
                filePath,
                _currentPrContext!.PullRequest.TargetRefName);

            if (item?.Content == null)
            {
                return $"File not found or has no content: {filePath}";
            }

            logger.LogInformation("Retrieved {Size} bytes for {FilePath}", item.Content.Length, filePath);
            return item.Content;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve file content for {FilePath}", filePath);
            return $"Error retrieving file: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets the content of a file from a specific commit or branch.
    /// Useful for comparing versions or understanding historical context.
    /// </summary>
    /// <param name="filePath">The path to the file</param>
    /// <param name="commitOrBranch">The commit SHA or branch name (e.g., "main", or a commit hash)</param>
    /// <returns>The file content at that version</returns>
    public async Task<string> GetFileAtCommitAsync(string filePath, string commitOrBranch)
    {
        EnsureContextSet();

        try
        {
            logger.LogDebug("AI requested file at commit: {FilePath} @ {Commit}", filePath, commitOrBranch);

            // If it looks like a branch name, prepend refs/heads/
            var versionDescriptor = commitOrBranch.StartsWith("refs/")
                ? commitOrBranch
                : commitOrBranch.Length == 40 // SHA hash
                    ? commitOrBranch
                    : $"refs/heads/{commitOrBranch}";

            var item = await adoClient.GetFileContentAsync(
                filePath,
                versionDescriptor);

            if (item?.Content == null)
            {
                return $"File not found at {commitOrBranch}: {filePath}";
            }

            logger.LogInformation("Retrieved {Size} bytes for {FilePath} @ {Commit}",
                item.Content.Length, filePath, commitOrBranch);
            return item.Content;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve file at commit: {FilePath} @ {Commit}", filePath, commitOrBranch);
            return $"Error retrieving file at {commitOrBranch}: {ex.Message}";
        }
    }

    /// <summary>
    /// Searches the codebase for files containing specific text or patterns.
    /// Useful for finding where functions, classes, or interfaces are defined or used.
    /// </summary>
    /// <param name="searchTerm">The text to search for (e.g., "class MyClass", "void MyMethod")</param>
    /// <param name="filePattern">Optional file pattern filter (e.g., "*.cs", "*.csproj")</param>
    /// <param name="maxResults">Maximum number of results to return (default: 10, max: 100)</param>
    /// <returns>List of files and locations where the term was found</returns>
    public async Task<string> SearchCodebaseAsync(string searchTerm, string? filePattern = null, int maxResults = FunctionDefaults.SearchDefaultMaxResults)
    {
        EnsureContextSet();

        // Clamp to reasonable limits
        maxResults = Math.Clamp(maxResults, 1, FunctionDefaults.SearchMaxResultsLimit);

        try
        {
            logger.LogDebug("AI searching codebase: '{SearchTerm}' (pattern: {Pattern}, maxResults: {MaxResults})",
                searchTerm, filePattern ?? "all", maxResults);

            var searchResults = await adoClient.SearchCodeAsync(
                searchTerm,
                _currentPrContext!.PullRequest.TargetRefName,
                filePattern,
                maxResults);

            if (searchResults.Count == 0)
            {
                return $"No results found for '{searchTerm}'";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {searchResults.Count} result(s) for '{searchTerm}':");

            foreach (var result in searchResults)
            {
                sb.AppendLine($"\n{result.FilePath} (line {result.LineNumber}):");
                sb.AppendLine(result.Context);
            }

            logger.LogInformation("Search returned {Count} results for '{SearchTerm}'", searchResults.Count, searchTerm);
            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to search codebase for '{SearchTerm}'", searchTerm);
            return $"Error searching codebase: {ex.Message}";
        }
    }

    /// <summary>
    /// Finds files that are related to the specified file through imports, references, or namespace usage.
    /// Useful for understanding the impact of changes.
    /// </summary>
    /// <param name="filePath">The file to find relations for</param>
    /// <returns>List of related files and how they're related</returns>
    public async Task<string> GetRelatedFilesAsync(string filePath)
    {
        EnsureContextSet();

        try
        {
            logger.LogDebug("AI requested related files for: {FilePath}", filePath);

            // For C# files, we can look for:
            // 1. Files in the same namespace
            // 2. Files that import this file's namespace
            // 3. Files in the same directory

            var fileContent = await GetFullFileContentAsync(filePath);
            if (fileContent.StartsWith("Error") || fileContent.StartsWith("File not found"))
            {
                return fileContent;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Related files for {filePath}:");

            // Extract namespace from the file
            var namespaceMatch = NamespaceRegex().Match(fileContent);
            if (namespaceMatch.Success)
            {
                var namespaceName = namespaceMatch.Groups[1].Value;
                sb.AppendLine($"\nFiles in same namespace '{namespaceName}':");

                var sameNamespaceFiles = await adoClient.SearchCodeAsync(
                    $"namespace {namespaceName}",
                    _currentPrContext!.PullRequest.TargetRefName,
                    "*.cs",
                    5);

                foreach (var file in sameNamespaceFiles.Where(f => f.FilePath != filePath))
                {
                    sb.AppendLine($"  - {file.FilePath}");
                }

                // Find files that import this namespace
                sb.AppendLine($"\nFiles importing '{namespaceName}':");
                var importingFiles = await adoClient.SearchCodeAsync(
                    $"using {namespaceName}",
                    _currentPrContext!.PullRequest.TargetRefName,
                    "*.cs",
                    5);

                foreach (var file in importingFiles.Where(f => f.FilePath != filePath))
                {
                    sb.AppendLine($"  - {file.FilePath}");
                }
            }

            logger.LogInformation("Found related files for {FilePath}", filePath);
            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get related files for {FilePath}", filePath);
            return $"Error finding related files: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets the commit history for a specific file.
    /// Useful for understanding how a file has evolved and who has worked on it.
    /// </summary>
    /// <param name="filePath">The path to the file</param>
    /// <param name="maxCommits">Maximum number of commits to return (default: 5, max: 30)</param>
    /// <returns>List of commits that modified this file</returns>
    public async Task<string> GetFileHistoryAsync(string filePath, int maxCommits = FunctionDefaults.FileHistoryDefaultMaxCommits)
    {
        EnsureContextSet();

        // Clamp to reasonable limits
        maxCommits = Math.Clamp(maxCommits, 1, FunctionDefaults.FileHistoryMaxCommitsLimit);

        try
        {
            logger.LogDebug("AI requested file history: {FilePath} (maxCommits: {MaxCommits})", filePath, maxCommits);

            var commits = await adoClient.GetFileHistoryAsync(
                filePath,
                _currentPrContext!.PullRequest.TargetRefName,
                maxCommits);

            if (commits.Count == 0)
            {
                return $"No commit history found for {filePath}";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Recent commits for {filePath}:");

            foreach (var commit in commits)
            {
                // When using LocalGitProvider, the Comment contains the full formatted string
                // Format: "SHA: Message (Author, Date)"
                // When using API, we format it from individual fields
                if (commit.CommitId != null)
                {
                    // API response with full commit details
                    var date = commit.Committer?.Date ?? commit.Author?.Date;
                    var author = commit.Committer?.Name ?? commit.Author?.Name ?? "Unknown";
                    sb.AppendLine($"\n{commit.CommitId[..Math.Min(8, commit.CommitId.Length)]} - {author} ({date:yyyy-MM-dd})");
                    sb.AppendLine($"  {commit.Comment}");
                }
                else
                {
                    // Local provider response (already formatted in Comment field)
                    sb.AppendLine($"\n{commit.Comment}");
                }
            }

            logger.LogInformation("Retrieved {Count} commits for {FilePath}", commits.Count, filePath);
            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get file history for {FilePath}", filePath);
            return $"Error getting file history: {ex.Message}";
        }
    }

    private void EnsureContextSet()
    {
        if (_currentPrContext == null)
        {
            throw new InvalidOperationException("Pull request context must be set before calling context retrieval functions.");
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"namespace\s+([\w\.]+)")]
    private static partial System.Text.RegularExpressions.Regex NamespaceRegex();
}
