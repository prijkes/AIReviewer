using AIReviewer.AzureDevOps;
using AIReviewer.Diff;
using Microsoft.Extensions.Logging;
using System.Text;

namespace AIReviewer.AI;

/// <summary>
/// Enriches diffs with additional context to improve AI review quality.
/// </summary>
public sealed class ContextEnricher
{
    private readonly ILogger<ContextEnricher> _logger;
    private readonly LocalGitProvider? _gitProvider;

    public ContextEnricher(ILogger<ContextEnricher> logger, LocalGitProvider? gitProvider = null)
    {
        _logger = logger;
        _gitProvider = gitProvider;
    }

    /// <summary>
    /// Enriches a diff with surrounding context from the target branch.
    /// Adds lines before and after changed sections for better understanding.
    /// </summary>
    public async Task<string> EnrichDiffWithContextAsync(ReviewFileDiff diff, string targetBranch, int contextLines = 5)
    {
        if (_gitProvider == null)
        {
            _logger.LogDebug("Git provider not available, returning original diff");
            return diff.DiffText;
        }

        try
        {
            // Get full file content from target branch
            var fullContent = await _gitProvider.GetFileContentAsync(diff.Path, targetBranch);
            if (fullContent == null)
            {
                _logger.LogDebug("Could not retrieve full file content for {Path}", diff.Path);
                return diff.DiffText;
            }

            var fileLines = fullContent.Split('\n');
            var diffLines = diff.DiffText.Split('\n');
            var enriched = new StringBuilder();
            var currentLine = 0;

            foreach (var diffLine in diffLines)
            {
                // Parse diff line to extract line number
                if (diffLine.StartsWith("@@"))
                {
                    // Extract target line number from unified diff header
                    var match = System.Text.RegularExpressions.Regex.Match(diffLine, @"\+(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var lineNum))
                    {
                        currentLine = lineNum - 1; // 0-based index
                        
                        // Add context before the change
                        var contextStart = Math.Max(0, currentLine - contextLines);
                        if (contextStart < currentLine)
                        {
                            enriched.AppendLine($"--- Context before (lines {contextStart + 1}-{currentLine}) ---");
                            for (int i = contextStart; i < currentLine && i < fileLines.Length; i++)
                            {
                                enriched.AppendLine($"  {fileLines[i]}");
                            }
                            enriched.AppendLine("--- End context ---");
                        }
                    }
                }

                enriched.AppendLine(diffLine);

                // Track current line for added lines
                if (diffLine.StartsWith("+") && !diffLine.StartsWith("+++"))
                {
                    currentLine++;
                }
            }

            var result = enriched.ToString();
            
            if (result.Length > diff.DiffText.Length)
            {
                _logger.LogDebug("Enriched diff for {Path} (+{ExtraBytes} bytes of context)", 
                    diff.Path, result.Length - diff.DiffText.Length);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enrich diff for {Path}, using original", diff.Path);
            return diff.DiffText;
        }
    }

    /// <summary>
    /// Analyzes the diff to extract key information like affected methods, classes, etc.
    /// </summary>
    public DiffAnalysis AnalyzeDiff(ReviewFileDiff diff)
    {
        var analysis = new DiffAnalysis();
        var lines = diff.DiffText.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart('+', '-', ' ');

            // Detect classes
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"(class|interface|struct|enum)\s+(\w+)"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"(class|interface|struct|enum)\s+(\w+)");
                analysis.AffectedTypes.Add(match.Groups[2].Value);
            }

            // Detect methods
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"(public|private|protected|internal)?\s*(static\s+)?(async\s+)?[\w<>]+\s+(\w+)\s*\("))
            {
                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"[\w<>]+\s+(\w+)\s*\(");
                if (match.Success)
                {
                    analysis.AffectedMethods.Add(match.Groups[1].Value);
                }
            }

            // Count changes
            if (line.StartsWith("+") && !line.StartsWith("+++"))
                analysis.AddedLines++;
            else if (line.StartsWith("-") && !line.StartsWith("---"))
                analysis.DeletedLines++;
        }

        analysis.NetChange = analysis.AddedLines - analysis.DeletedLines;
        analysis.TotalChanges = analysis.AddedLines + analysis.DeletedLines;

        return analysis;
    }

    /// <summary>
    /// Generates a summary of the changes for inclusion in the review prompt.
    /// </summary>
    public string GenerateChangeSummary(DiffAnalysis analysis)
    {
        var summary = new StringBuilder();
        summary.AppendLine("Change Summary:");
        summary.AppendLine($"  Lines added: {analysis.AddedLines}");
        summary.AppendLine($"  Lines deleted: {analysis.DeletedLines}");
        summary.AppendLine($" Net change: {analysis.NetChange:+#;-#;0}");

        if (analysis.AffectedTypes.Count > 0)
        {
            summary.AppendLine($"  Affected types: {string.Join(", ", analysis.AffectedTypes.Take(5))}");
            if (analysis.AffectedTypes.Count > 5)
                summary.AppendLine($"    ... and {analysis.AffectedTypes.Count - 5} more");
        }

        if (analysis.AffectedMethods.Count > 0)
        {
            summary.AppendLine($"  Affected methods: {string.Join(", ", analysis.AffectedMethods.Take(5))}");
            if (analysis.AffectedMethods.Count > 5)
                summary.AppendLine($"    ... and {analysis.AffectedMethods.Count - 5} more");
        }

        return summary.ToString();
    }
}

/// <summary>
/// Contains analysis results from a diff.
/// </summary>
public sealed class DiffAnalysis
{
    public int AddedLines { get; set; }
    public int DeletedLines { get; set; }
    public int NetChange { get; set; }
    public int TotalChanges { get; set; }
    public HashSet<string> AffectedTypes { get; } = [];
    public HashSet<string> AffectedMethods { get; } = [];
}
