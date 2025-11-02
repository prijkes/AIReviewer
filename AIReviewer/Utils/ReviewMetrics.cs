using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AIReviewer.Utils;

/// <summary>
/// Tracks and logs metrics for code review operations.
/// </summary>
public sealed class ReviewMetrics
{
    private readonly ILogger<ReviewMetrics> _logger;
    private readonly Stopwatch _overallStopwatch;
    private int _totalFilesProcessed;
    private int _totalIssuesFound;
    private int _totalErrors;
    private int _totalWarnings;
    private long _totalInputTokens;
    private long _totalOutputTokens;
    private readonly List<FileReviewMetric> _fileMetrics = [];

    public ReviewMetrics(ILogger<ReviewMetrics> logger)
    {
        _logger = logger;
        _overallStopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Records metrics for a single file review.
    /// </summary>
    public void RecordFileReview(string filePath, int issuesFound, int errors, int warnings, long inputTokens, long outputTokens, long durationMs)
    {
        _totalFilesProcessed++;
        _totalIssuesFound += issuesFound;
        _totalErrors += errors;
        _totalWarnings += warnings;
        _totalInputTokens += inputTokens;
        _totalOutputTokens += outputTokens;

        var metric = new FileReviewMetric
        {
            FilePath = filePath,
            IssuesFound = issuesFound,
            Errors = errors,
            Warnings = warnings,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            DurationMs = durationMs
        };

        _fileMetrics.Add(metric);

        _logger.LogInformation(
            "File review completed - Path: {FilePath}, Issues: {Issues} (E:{Errors}/W:{Warnings}), Tokens: {InputTokens}→{OutputTokens}, Duration: {Duration}ms",
            filePath, issuesFound, errors, warnings, inputTokens, outputTokens, durationMs);
    }

    /// <summary>
    /// Records token usage without issue counts (for metadata reviews).
    /// </summary>
    public void RecordTokenUsage(string operation, long inputTokens, long outputTokens, long durationMs)
    {
        _totalInputTokens += inputTokens;
        _totalOutputTokens += outputTokens;

        _logger.LogInformation(
            "Operation: {Operation}, Tokens: {InputTokens}→{OutputTokens}, Duration: {Duration}ms",
            operation, inputTokens, outputTokens, durationMs);
    }

    /// <summary>
    /// Logs the final summary of all review metrics.
    /// </summary>
    public void LogSummary()
    {
        _overallStopwatch.Stop();
        var totalDuration = _overallStopwatch.ElapsedMilliseconds;
        var totalTokens = _totalInputTokens + _totalOutputTokens;
        var avgDurationPerFile = _totalFilesProcessed > 0 ? totalDuration / _totalFilesProcessed : 0;
        var avgTokensPerFile = _totalFilesProcessed > 0 ? totalTokens / _totalFilesProcessed : 0;

        _logger.LogInformation(
            @"
=== Review Metrics Summary ===
Total Duration: {TotalDuration}ms
Files Processed: {FilesProcessed}
Total Issues: {TotalIssues} (Errors: {Errors}, Warnings: {Warnings})
Token Usage:
  - Input Tokens: {InputTokens:N0}
  - Output Tokens: {OutputTokens:N0}
  - Total Tokens: {TotalTokens:N0}
Performance:
  - Avg Duration/File: {AvgDuration}ms
  - Avg Tokens/File: {AvgTokens:N0}
Cost Estimate (GPT-4):
  - Input Cost: ${InputCost:F4} ({InputTokensFormatted:N0} tokens @ $0.03/1K)
  - Output Cost: ${OutputCost:F4} ({OutputTokensFormatted:N0} tokens @ $0.06/1K)
  - Total Cost: ${TotalCost:F4}
==============================",
            totalDuration,
            _totalFilesProcessed,
            _totalIssuesFound,
            _totalErrors,
            _totalWarnings,
            _totalInputTokens,
            _totalOutputTokens,
            totalTokens,
            avgDurationPerFile,
            avgTokensPerFile,
            (_totalInputTokens / 1000.0) * 0.03,
            _totalInputTokens,
            (_totalOutputTokens / 1000.0) * 0.06,
            _totalOutputTokens,
            (_totalInputTokens / 1000.0) * 0.03 + (_totalOutputTokens / 1000.0) * 0.06
        );

        // Log top 5 most expensive files by tokens
        if (_fileMetrics.Count > 0)
        {
            var topExpensive = _fileMetrics
                .OrderByDescending(m => m.InputTokens + m.OutputTokens)
                .Take(5)
                .ToList();

            _logger.LogInformation("Top 5 most expensive files by token usage:");
            foreach (var metric in topExpensive)
            {
                var totalFileTokens = metric.InputTokens + metric.OutputTokens;
                _logger.LogInformation(
                    "  {FilePath}: {TotalTokens:N0} tokens ({InputTokens:N0}→{OutputTokens:N0}), {Issues} issues",
                    metric.FilePath, totalFileTokens, metric.InputTokens, metric.OutputTokens, metric.IssuesFound);
            }
        }

        // Log slowest files
        if (_fileMetrics.Count > 0)
        {
            var slowest = _fileMetrics
                .OrderByDescending(m => m.DurationMs)
                .Take(5)
                .ToList();

            _logger.LogInformation("Top 5 slowest file reviews:");
            foreach (var metric in slowest)
            {
                _logger.LogInformation(
                    "  {FilePath}: {Duration}ms, {Issues} issues",
                    metric.FilePath, metric.DurationMs, metric.IssuesFound);
            }
        }
    }

    /// <summary>
    /// Gets the total duration of the review process.
    /// </summary>
    public long GetTotalDurationMs() => _overallStopwatch.ElapsedMilliseconds;

    /// <summary>
    /// Gets the total number of tokens used.
    /// </summary>
    public long GetTotalTokens() => _totalInputTokens + _totalOutputTokens;

    private sealed class FileReviewMetric
    {
        public required string FilePath { get; init; }
        public int IssuesFound { get; init; }
        public int Errors { get; init; }
        public int Warnings { get; init; }
        public long InputTokens { get; init; }
        public long OutputTokens { get; init; }
        public long DurationMs { get; init; }
    }
}
