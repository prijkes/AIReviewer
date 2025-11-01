using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AIReviewer.AzureDevOps;

/// <summary>
/// Provides local filesystem and git command access for file operations.
/// Used when LOCAL_REPO_PATH is configured to avoid Azure DevOps API calls.
/// </summary>
public sealed class LocalGitProvider
{
    private readonly ILogger<LocalGitProvider> _logger;
    private readonly string _repoPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalGitProvider"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="repoPath">Path to the local git repository.</param>
    /// <exception cref="DirectoryNotFoundException">Thrown when repository path doesn't exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when path is not a git repository.</exception>
    public LocalGitProvider(ILogger<LocalGitProvider> logger, string repoPath)
    {
        _logger = logger;
        _repoPath = repoPath;

        if (!Directory.Exists(_repoPath))
        {
            throw new DirectoryNotFoundException($"Local repository path does not exist: {_repoPath}");
        }

        if (!Directory.Exists(Path.Combine(_repoPath, ".git")))
        {
            throw new InvalidOperationException($"Path is not a git repository (no .git directory): {_repoPath}");
        }

        _logger.LogInformation("Local git provider initialized at: {RepoPath}", _repoPath);
    }

    /// <summary>
    /// Gets the content of a file at a specific version (commit or branch).
    /// </summary>
    /// <param name="filePath">The path to the file relative to repository root.</param>
    /// <param name="versionDescriptor">The version (commit SHA or branch name).</param>
    /// <returns>The file content as a string, or null if file doesn't exist.</returns>
    public async Task<string?> GetFileContentAsync(string filePath, string versionDescriptor)
    {
        try
        {
            // Clean the file path
            filePath = filePath.TrimStart('/').Replace('\\', '/');
            
            // Use git show to get file content at specific version
            var gitCommand = $"show {versionDescriptor}:{filePath}";
            var result = await RunGitCommandAsync(gitCommand);
            
            if (result.ExitCode == 0)
            {
                return result.Output;
            }

            _logger.LogWarning("Failed to get file content for {FilePath} at {Version}: {Error}", 
                filePath, versionDescriptor, result.Error);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file content for {FilePath} at {Version}", filePath, versionDescriptor);
            return null;
        }
    }

    /// <summary>
    /// Searches the codebase for files containing specific text using git grep.
    /// </summary>
    /// <param name="searchTerm">The text to search for.</param>
    /// <param name="versionDescriptor">The version (branch name or commit SHA) to search in.</param>
    /// <param name="filePattern">Optional file pattern filter (e.g., "*.cs").</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <returns>List of search results with file path, line number, and context.</returns>
    public async Task<List<CodeSearchResult>> SearchCodeAsync(
        string searchTerm,
        string versionDescriptor,
        string? filePattern,
        int maxResults)
    {
        var results = new List<CodeSearchResult>();

        try
        {
            // Build git grep command
            var gitCommand = new StringBuilder("grep -n -i");
            
            // Add context lines (1 before and 1 after)
            gitCommand.Append(" -C 1");
            
            // Add the search term (escaped)
            gitCommand.Append($" \"{EscapeGitArgument(searchTerm)}\"");
            
            // Add version
            gitCommand.Append($" {versionDescriptor}");
            
            // Add file pattern if specified
            if (!string.IsNullOrWhiteSpace(filePattern))
            {
                gitCommand.Append($" -- \"{filePattern}\"");
            }

            var result = await RunGitCommandAsync(gitCommand.ToString());
            
            if (result.ExitCode != 0 && !string.IsNullOrEmpty(result.Error))
            {
                // Exit code 1 just means no matches found, which is valid
                if (result.ExitCode != 1)
                {
                    _logger.LogWarning("Git grep failed: {Error}", result.Error);
                }
                return results;
            }

            // Parse git grep output: format is "file:line:content"
            var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var currentFile = string.Empty;
            var contextLines = new StringBuilder();
            var lineNumber = 0;

            foreach (var line in lines)
            {
                if (results.Count >= maxResults) break;

                // Parse git grep output
                var match = Regex.Match(line, @"^([^:]+):(\d+):(.*)$");
                if (match.Success)
                {
                    var file = match.Groups[1].Value.Trim();
                    var lineNum = int.Parse(match.Groups[2].Value);
                    var content = match.Groups[3].Value;

                    // If this is a new match (not context), save previous and start new
                    if (file != currentFile || Math.Abs(lineNum - lineNumber) > 2)
                    {
                        if (contextLines.Length > 0 && !string.IsNullOrEmpty(currentFile))
                        {
                            results.Add(new CodeSearchResult(currentFile, lineNumber, contextLines.ToString()));
                        }

                        currentFile = file;
                        lineNumber = lineNum;
                        contextLines.Clear();
                    }

                    contextLines.AppendLine(content);
                    lineNumber = lineNum;
                }
            }

            // Add the last result
            if (contextLines.Length > 0 && !string.IsNullOrEmpty(currentFile))
            {
                results.Add(new CodeSearchResult(currentFile, lineNumber, contextLines.ToString()));
            }

            _logger.LogDebug("Search found {Count} results for '{SearchTerm}'", results.Count, searchTerm);
            return results.Take(maxResults).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching codebase for '{SearchTerm}'", searchTerm);
            return results;
        }
    }

    /// <summary>
    /// Gets the commit history for a specific file using git log.
    /// </summary>
    /// <param name="filePath">The path to the file relative to repository root.</param>
    /// <param name="versionDescriptor">The version (branch name) to get history from.</param>
    /// <param name="maxCommits">Maximum number of commits to return.</param>
    /// <returns>List of commits in "SHA: Message (Author, Date)" format.</returns>
    public async Task<List<string>> GetFileHistoryAsync(
        string filePath,
        string versionDescriptor,
        int maxCommits)
    {
        var results = new List<string>();

        try
        {
            // Clean the file path
            filePath = filePath.TrimStart('/').Replace('\\', '/');

            // Build git log command
            var gitCommand = $"log {versionDescriptor} -n {maxCommits} --pretty=format:\"%H|%an|%ad|%s\" --date=short -- \"{filePath}\"";
            
            var result = await RunGitCommandAsync(gitCommand);
            
            if (result.ExitCode != 0)
            {
                _logger.LogWarning("Failed to get file history for {FilePath}: {Error}", filePath, result.Error);
                return results;
            }

            // Parse git log output
            var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length >= 4)
                {
                    var sha = parts[0].Substring(0, Math.Min(8, parts[0].Length)); // Short SHA
                    var author = parts[1];
                    var date = parts[2];
                    var message = parts[3];
                    
                    results.Add($"{sha}: {message} ({author}, {date})");
                }
            }

            _logger.LogDebug("Found {Count} commits for {FilePath}", results.Count, filePath);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file history for {FilePath}", filePath);
            return results;
        }
    }

    /// <summary>
    /// Runs a git command in the repository directory.
    /// </summary>
    /// <param name="arguments">Git command arguments (without 'git' prefix).</param>
    /// <returns>Command result with exit code, output, and error.</returns>
    private async Task<GitCommandResult> RunGitCommandAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return new GitCommandResult(
            process.ExitCode,
            outputBuilder.ToString(),
            errorBuilder.ToString()
        );
    }

    /// <summary>
    /// Escapes special characters in git command arguments.
    /// </summary>
    private static string EscapeGitArgument(string argument)
    {
        return argument.Replace("\"", "\\\"").Replace("$", "\\$");
    }

    /// <summary>
    /// Result of a git command execution.
    /// </summary>
    private sealed record GitCommandResult(int ExitCode, string Output, string Error);
}

/// <summary>
/// Represents a code search result with file path, line number, and surrounding context.
/// </summary>
public sealed record CodeSearchResult(string FilePath, int LineNumber, string Context);
