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
public sealed record FileDiff(string Path, string DiffText, string FileHash, bool IsBinary);

/// <summary>
/// Service for retrieving and processing file diffs from Azure DevOps pull request iterations.
/// </summary>
public sealed class DiffService
{
    private readonly ILogger<DiffService> _logger;
    private readonly AdoSdkClient _adoClient;
    private readonly ReviewerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiffService"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="adoClient">Client for Azure DevOps operations.</param>
    /// <param name="options">Configuration options for the reviewer.</param>
    public DiffService(ILogger<DiffService> logger, AdoSdkClient adoClient, IOptionsMonitor<ReviewerOptions> options)
    {
        _logger = logger;
        _adoClient = adoClient;
        _options = options.CurrentValue;
    }

    /// <summary>
    /// Retrieves and processes all file diffs for a specific pull request iteration.
    /// Skips binary files and truncates large diffs according to configuration limits.
    /// </summary>
    /// <param name="pr">The pull request context.</param>
    /// <param name="iteration">The iteration to get diffs for.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A read-only list of file diffs ready for AI review.</returns>
    public async Task<IReadOnlyList<FileDiff>> GetDiffsAsync(PullRequestContext pr, GitPullRequestIteration iteration, CancellationToken cancellationToken)
    {
        var changes = await _adoClient.GetIterationChangesAsync(pr.Repository.Id, pr.PullRequest.PullRequestId, iteration.Id, cancellationToken);
        var diffs = new List<FileDiff>();

        foreach (var change in changes.Changes)
        {
            if (change.Item is not GitItem gitItem)
            {
                continue;
            }

            var path = gitItem.Path ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var isBinary = change.ChangeTrackingId == null && gitItem.GitObjectType == GitObjectType.Blob;
            if (isBinary)
            {
                _logger.LogInformation("Skipping binary file {Path}", path);
                continue;
            }

            var diff = await _adoClient.Git.GetPullRequestIterationDiffsAsync(pr.Repository.Id, pr.PullRequest.PullRequestId, iteration.Id, new GitPullRequestDiffOptions
            {
                BaseVersion = iteration.BaseRefCommit!,
                TargetVersion = iteration.TargetRefCommit!
            }, cancellationToken: cancellationToken);

            var textDiff = string.Join(Environment.NewLine, diff.Changes.Select(dc => dc.ChangeTrackingId));

            var trimmedDiff = textDiff.Length > _options.MaxDiffBytes
                ? textDiff[.._options.MaxDiffBytes]
                : textDiff;

            var fileHash = Logging.HashSha256($"{iteration.Id}:{path}:{trimmedDiff}");

            diffs.Add(new FileDiff(path, trimmedDiff, fileHash, false));
        }

        _logger.LogInformation("Prepared {Count} diffs for iteration {IterationId}", diffs.Count, iteration.Id);
        return diffs;
    }
}
