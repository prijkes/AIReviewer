using AIReviewer.AzureDevOps;
using AIReviewer.AzureDevOps.Models;
using AIReviewer.Options;
using AIReviewer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AIReviewer.Diff;

public sealed record FileDiff(string Path, string DiffText, string FileHash, bool IsBinary);

public sealed class DiffService
{
    private readonly ILogger<DiffService> _logger;
    private readonly AdoSdkClient _adoClient;
    private readonly ReviewerOptions _options;

    public DiffService(ILogger<DiffService> logger, AdoSdkClient adoClient, IOptionsMonitor<ReviewerOptions> options)
    {
        _logger = logger;
        _adoClient = adoClient;
        _options = options.CurrentValue;
    }

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
