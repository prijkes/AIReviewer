using Quaaly.Infrastructure.AzureDevOps;
using Quaaly.Infrastructure.AzureDevOps.Models;
using Quaaly.Infrastructure.Options;
using Quaaly.Infrastructure.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace Quaaly.Infrastructure.Diff;

/// <summary>
/// Represents a file diff with its content and metadata.
/// </summary>
/// <param name="Path">The file path.</param>
/// <param name="DiffText">The unified diff text showing changes.</param>
/// <param name="FileHash">A hash of the file content for fingerprinting.</param>
/// <param name="IsBinary">Indicates whether this is a binary file.</param>
/// <param name="IsDeleted">Indicates whether this file was deleted in the PR.</param>
public sealed record ReviewFileDiff(string Path, string DiffText, string FileHash, bool IsBinary, bool IsDeleted);

/// <summary>
/// Service for retrieving and processing file diffs from Azure DevOps pull request iterations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DiffService"/> class.
/// </remarks>
/// <param name="logger">Logger for diagnostic information.</param>
/// <param name="adoClient">Client for Azure DevOps operations.</param>
/// <param name="options">Configuration options for the reviewer.</param>
public sealed class DiffService(ILogger<DiffService> logger, IAdoSdkClient adoClient, IOptionsMonitor<ReviewerOptions> options)
{
    private readonly ReviewerOptions _options = options.CurrentValue;

    /// <summary>
    /// Retrieves and processes all file diffs for a specific pull request iteration.
    /// Skips binary files and truncates large diffs according to configuration limits.
    /// </summary>
    /// <param name="pr">The pull request context.</param>
    /// <param name="iteration">The iteration to get diffs for.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A read-only list of file diffs ready for AI review.</returns>
    public async Task<IReadOnlyList<ReviewFileDiff>> GetDiffsAsync(PullRequestContext pr, GitPullRequestIteration iteration, CancellationToken cancellationToken)
    {
        var iterationId = iteration.Id ?? throw new InvalidOperationException("Iteration ID is required");
        var changes = await adoClient.GetIterationChangesAsync(pr.Repository.Id, pr.PullRequest.PullRequestId, iterationId, cancellationToken);
        var diffs = new List<ReviewFileDiff>();

        // Get the base and target commits for diff generation
        var baseCommit = pr.PullRequest.LastMergeTargetCommit?.CommitId;
        var targetCommit = pr.PullRequest.LastMergeSourceCommit?.CommitId;

        if (string.IsNullOrEmpty(baseCommit) || string.IsNullOrEmpty(targetCommit))
        {
            logger.LogWarning("Cannot generate diffs: base or target commit is missing");
            return diffs;
        }

        foreach (var change in changes.ChangeEntries ?? [])
        {
            if (change.Item is not GitItem gitItem) continue;

            // For deleted files, path is empty - use OriginalPath instead
            var path = gitItem.Path ?? change.OriginalPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                logger.LogDebug("Skipping change entry with no path or originalPath");
                continue;
            }

            // Determine if this is a deletion (GitObjectType.Bad means file doesn't exist in target)
            var isDeleted = gitItem.GitObjectType == GitObjectType.Bad;

            // Check if binary based on file type (but allow deletions through)
            // For deleted files, we can't check the blob, so we'll let the diff tell us if it's binary
            var isBinary = !isDeleted && gitItem.GitObjectType != GitObjectType.Blob;
            if (isBinary)
            {
                logger.LogInformation("Skipping non-blob file {Path}", path);
                continue;
            }

            if (isDeleted)
            {
                logger.LogDebug("Processing deleted file {Path}", path);
            }

            // Generate the actual diff using git
            var textDiff = await adoClient.GetFileDiffAsync(path, baseCommit, targetCommit, cancellationToken);

            if (string.IsNullOrEmpty(textDiff))
            {
                logger.LogDebug("No diff content for {Path}, skipping", path);
                continue;
            }

            // Truncate if needed and log
            var trimmedDiff = textDiff;
            if (textDiff.Length > _options.MaxDiffBytes)
            {
                trimmedDiff = textDiff[.._options.MaxDiffBytes];
                logger.LogWarning("Truncating large diff for {Path} ({Original} bytes -> {Truncated} bytes)",
                    path, textDiff.Length, _options.MaxDiffBytes);
            }

            var fileHash = Logging.HashSha256($"{iterationId}:{path}:{trimmedDiff}");

            diffs.Add(new ReviewFileDiff(path, trimmedDiff, fileHash, false, isDeleted));
        }

        var totalBytes = diffs.Sum(d => d.DiffText.Length);
        logger.LogInformation("Prepared {Count} diffs for iteration {IterationId} (total: {TotalBytes} bytes)",
            diffs.Count, iterationId, totalBytes);
        return diffs;
    }
}
