using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Quaally.AzureDevOps;

/// <summary>
/// Provides local git repository access using LibGit2Sharp.
/// Used when LOCAL_REPO_PATH is configured to avoid Azure DevOps API calls.
/// </summary>
public sealed class LocalGitProvider : IDisposable
{
    private readonly ILogger<LocalGitProvider> _logger;
    private readonly string _repoPath;
    private readonly Repository _repository;

    public LocalGitProvider(ILogger<LocalGitProvider> logger, string repoPath)
    {
        _logger = logger;
        _repoPath = repoPath;

        if (!Directory.Exists(_repoPath))
        {
            throw new DirectoryNotFoundException($"Local repository path does not exist: {_repoPath}");
        }

        try
        {
            _repository = new Repository(_repoPath);
            _logger.LogInformation("Local git provider initialized at: {RepoPath}", _repoPath);
        }
        catch (RepositoryNotFoundException ex)
        {
            throw new InvalidOperationException(
                $"Path is not a git repository: {_repoPath}. Ensure the directory contains a .git folder.", ex);
        }
    }

    /// <summary>
    /// Gets the content of a file at a specific version (commit or branch).
    /// </summary>
    public Task<string?> GetFileContentAsync(string filePath, string versionDescriptor)
    {
        return Task.Run(() => GetFileContent(filePath, versionDescriptor));
    }

    private string? GetFileContent(string filePath, string versionDescriptor)
    {
        try
        {
            filePath = NormalizePath(filePath);

            var commit = ResolveCommit(versionDescriptor);
            if (commit == null)
            {
                _logger.LogWarning("Failed to resolve version: {Version}", versionDescriptor);
                return null;
            }

            var treeEntry = commit[filePath];
            if (treeEntry?.TargetType != TreeEntryTargetType.Blob)
            {
                _logger.LogWarning("File not found or not a blob: {FilePath} at {Version}", filePath, versionDescriptor);
                return null;
            }

            var blob = (Blob)treeEntry.Target;
            var content = blob.GetContentText();

            _logger.LogDebug("Retrieved {Size} bytes for {FilePath}", content.Length, filePath);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file: {FilePath} at {Version}", filePath, versionDescriptor);
            return null;
        }
    }

    /// <summary>
    /// Searches the codebase for files containing specific text using parallel processing.
    /// </summary>
    public Task<List<CodeSearchResult>> SearchCodeAsync(
        string searchTerm,
        string versionDescriptor,
        string? filePattern,
        int maxResults)
    {
        return Task.Run(() => SearchCode(searchTerm, versionDescriptor, filePattern, maxResults));
    }

    /// <summary>
    /// Gets the commit history for a specific file.
    /// </summary>
    public Task<List<string>> GetFileHistoryAsync(
        string filePath,
        string versionDescriptor,
        int maxCommits)
    {
        return Task.Run(() => GetFileHistory(filePath, versionDescriptor, maxCommits));
    }

    /// <summary>
    /// Gets the unified diff for a file between two commits.
    /// </summary>
    public Task<string?> GetFileDiffAsync(
        string filePath,
        string baseCommit,
        string targetCommit)
    {
        return Task.Run(() => GetFileDiff(filePath, baseCommit, targetCommit));
    }

    private List<CodeSearchResult> SearchCode(
        string searchTerm,
        string versionDescriptor,
        string? filePattern,
        int maxResults)
    {
        try
        {
            var commit = ResolveCommit(versionDescriptor);
            if (commit == null)
            {
                _logger.LogWarning("Failed to resolve version: {Version}", versionDescriptor);
                return [];
            }

            var filePatternRegex = CompileFilePattern(filePattern);
            var allFiles = CollectFiles(commit.Tree);

            _logger.LogDebug("Searching {FileCount} files in parallel for: '{SearchTerm}'",
                allFiles.Count, searchTerm);

            var results = SearchFilesInParallel(allFiles, searchTerm, filePatternRegex, maxResults);

            _logger.LogInformation("Found {ResultCount} results for '{SearchTerm}' (searched {FileCount} files)",
                results.Count, searchTerm, allFiles.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for: '{SearchTerm}'", searchTerm);
            return [];
        }
    }

    private List<CodeSearchResult> SearchFilesInParallel(
        List<FileEntry> files,
        string searchTerm,
        Regex? filePattern,
        int maxResults)
    {
        var results = new ConcurrentBag<CodeSearchResult>();
        var cancellation = new CancellationTokenSource();

        Parallel.ForEach(
            files,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellation.Token
            },
            (file, state) =>
            {
                try
                {
                    // Early exit if we have enough results
                    if (results.Count >= maxResults)
                    {
                        cancellation.Cancel();
                        return;
                    }

                    // Apply file pattern filter
                    if (filePattern != null && !filePattern.IsMatch(file.Path)) return;

                    // Skip binary files
                    if (file.Blob.IsBinary) return;

                    // Search file content
                    var fileResults = SearchFileContent(file, searchTerm, maxResults - results.Count);
                    foreach (var result in fileResults)
                    {
                        if (results.Count < maxResults) results.Add(result);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to search: {FilePath}", file.Path);
                }
            });

        return [.. results
            .Take(maxResults)
            .OrderBy(r => r.FilePath)
            .ThenBy(r => r.LineNumber)];
    }

    private static List<CodeSearchResult> SearchFileContent(FileEntry file, string searchTerm, int maxMatches)
    {
        var results = new List<CodeSearchResult>();
        var content = file.Blob.GetContentText();
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length && results.Count < maxMatches; i++)
        {
            if (lines[i].Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                var context = BuildContext(lines, i);
                results.Add(new CodeSearchResult(file.Path, i + 1, context));
            }
        }

        return results;
    }

    private static string BuildContext(string[] lines, int lineIndex)
    {
        var context = new StringBuilder();

        if (lineIndex > 0)
            context.AppendLine(lines[lineIndex - 1]);

        context.AppendLine(lines[lineIndex]);

        if (lineIndex < lines.Length - 1)
            context.AppendLine(lines[lineIndex + 1]);

        return context.ToString();
    }

    private static List<FileEntry> CollectFiles(Tree tree)
    {
        var files = new List<FileEntry>();
        CollectFilesRecursive(tree, "", files);
        return files;
    }

    private static void CollectFilesRecursive(Tree tree, string currentPath, List<FileEntry> files)
    {
        foreach (var entry in tree)
        {
            var fullPath = string.IsNullOrEmpty(currentPath)
                ? entry.Name
                : $"{currentPath}/{entry.Name}";

            switch (entry.TargetType)
            {
                case TreeEntryTargetType.Tree:
                    CollectFilesRecursive((Tree)entry.Target, fullPath, files);
                    break;

                case TreeEntryTargetType.Blob:
                    files.Add(new FileEntry(fullPath, (Blob)entry.Target));
                    break;
            }
        }
    }

    private List<string> GetFileHistory(string filePath, string versionDescriptor, int maxCommits)
    {
        try
        {
            filePath = NormalizePath(filePath);

            var commit = ResolveCommit(versionDescriptor);
            if (commit == null)
            {
                _logger.LogWarning("Failed to resolve version: {Version}", versionDescriptor);
                return [];
            }

            var filter = new CommitFilter
            {
                IncludeReachableFrom = commit,
                SortBy = CommitSortStrategies.Time
            };

            var commits = _repository.Commits
                .QueryBy(filePath, filter)
                .Take(maxCommits)
                .Select(FormatCommit)
                .ToList();

            _logger.LogDebug("Found {Count} commits for: {FilePath}", commits.Count, filePath);
            return commits;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history for: {FilePath}", filePath);
            return [];
        }
    }

    private string? GetFileDiff(string filePath, string baseCommit, string targetCommit)
    {
        try
        {
            filePath = NormalizePath(filePath);

            var baseCommitObj = ResolveCommit(baseCommit);
            var targetCommitObj = ResolveCommit(targetCommit);

            if (baseCommitObj == null || targetCommitObj == null)
            {
                _logger.LogWarning("Failed to resolve commits for diff: {Base} -> {Target}", baseCommit, targetCommit);
                return null;
            }

            // Get the patch for the file
            var patch = _repository.Diff.Compare<Patch>(baseCommitObj.Tree, targetCommitObj.Tree, new[] { filePath });

            if (patch == null || !patch.Any())
            {
                _logger.LogDebug("No diff found for {FilePath} between {Base} and {Target}", filePath, baseCommit, targetCommit);
                return string.Empty; // File unchanged
            }

            var diff = patch.Content;
            _logger.LogDebug("Generated {Size} bytes diff for {FilePath}", diff.Length, filePath);
            return diff;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting diff for: {FilePath} ({Base} -> {Target})", filePath, baseCommit, targetCommit);
            return null;
        }
    }

    private static string FormatCommit(LogEntry entry)
    {
        var commit = entry.Commit;
        var shortSha = commit.Sha[..Math.Min(8, commit.Sha.Length)];
        var author = commit.Author.Name;
        var date = commit.Author.When.ToString("yyyy-MM-dd");
        var message = commit.MessageShort;

        return $"{shortSha}: {message} ({author}, {date})";
    }

    private Commit? ResolveCommit(string versionDescriptor)
    {
        try
        {
            var version = versionDescriptor.StartsWith("refs/heads/")
                ? versionDescriptor["refs/heads/".Length..]
                : versionDescriptor;

            // Try direct lookup
            if (_repository.Lookup(version) is Commit commit)
                return commit;

            if (_repository.Lookup(version) is TagAnnotation tag)
                return tag.Target as Commit;

            // Try branch lookup
            var branch = _repository.Branches[version];
            if (branch != null)
                return branch.Tip;

            _logger.LogWarning("Could not resolve version: {Version}", versionDescriptor);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving version: {Version}", versionDescriptor);
            return null;
        }
    }

    private static Regex? CompileFilePattern(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return null;

        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private static string NormalizePath(string path)
    {
        return path.TrimStart('/').Replace('\\', '/');
    }

    public void Dispose()
    {
        _repository?.Dispose();
    }

    private record FileEntry(string Path, Blob Blob);
}

public sealed record CodeSearchResult(string FilePath, int LineNumber, string Context);
