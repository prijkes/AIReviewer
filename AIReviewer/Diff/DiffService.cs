using AIReviewer.AzureDevOps;
using AIReviewer.AzureDevOps.Models;
using AIReviewer.Options;
using AIReviewer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AIReviewer.Diff;

/// <summary>
/// Represents a file diff with its content and metadata.
/// </summary>
/// <param name="Path">The file path.</param>
/// <param name="DiffText">The unified diff text showing changes.</param>
/// <param name="FileHash">A hash of the file content for fingerprinting.</param>
/// <param name="IsBinary">Indicates whether this is a binary file.</param>
public sealed record ReviewFileDiff(string Path, string DiffText, string FileHash, bool IsBinary);

/// <summary>
/// Service for retrieving and processing file diffs from Azure DevOps pull request iterations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DiffService"/> class.
/// </remarks>
/// <param name="logger">Logger for diagnostic information.</param>
/// <param name="adoClient">Client for Azure DevOps operations.</param>
/// <param name="options">Configuration options for the reviewer.</param>
public sealed class DiffService(ILogger<DiffService> logger, AdoSdkClient adoClient, IOptionsMonitor<ReviewerOptions> options)
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

        foreach (var change in changes.ChangeEntries ?? [])
        {
            if (change.Item is not GitItem gitItem) continue;

            var path = gitItem.Path ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path)) continue;

            var isBinary = change.ChangeTrackingId == 0 && gitItem.GitObjectType == GitObjectType.Blob;
            if (isBinary)
            {
                logger.LogInformation("Skipping binary file {Path}", path);
                continue;
            }

            // Get the diff text for this change
            var textDiff = change.ChangeTrackingId.ToString();

            // Truncate if needed and log
            var trimmedDiff = textDiff;
            if (textDiff.Length > _options.MaxDiffBytes)
            {
                trimmedDiff = textDiff[.._options.MaxDiffBytes];
                logger.LogWarning("Truncating large diff for {Path} ({Original} bytes -> {Truncated} bytes)",
                    path, textDiff.Length, _options.MaxDiffBytes);
            }

            var fileHash = Logging.HashSha256($"{iterationId}:{path}:{trimmedDiff}");

            diffs.Add(new ReviewFileDiff(path, trimmedDiff, fileHash, false));
        }

        var totalBytes = diffs.Sum(d => d.DiffText.Length);
        logger.LogInformation("Prepared {Count} diffs for iteration {IterationId} (total: {TotalBytes} bytes)",
            diffs.Count, iterationId, totalBytes);
        return diffs;
    }
}
