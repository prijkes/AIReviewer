using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace Quaally.Infrastructure.Diff;

/// <summary>
/// Intelligently splits large diffs into smaller, logical chunks for review.
/// Uses language-agnostic strategies based on diff structure rather than code syntax.
/// </summary>
public sealed partial class DiffChunker(ILogger<DiffChunker> logger)
{
    // Pattern to identify diff hunk headers (e.g., @@ -10,5 +10,7 @@)
    private static readonly Regex DiffHunkHeaderPattern = DiffHunkHeaderRegex();

    /// <summary>
    /// Splits a large diff into logical chunks based on diff structure.
    /// This implementation is language-agnostic and works with any programming language.
    /// </summary>
    /// <param name="diff">The diff to chunk.</param>
    /// <param name="maxChunkSize">Maximum size of each chunk in bytes.</param>
    /// <returns>List of diff chunks with contextual information.</returns>
    public List<DiffChunk> ChunkDiff(ReviewFileDiff diff, int maxChunkSize)
    {
        if (diff.DiffText.Length <= maxChunkSize)
        {
            // No need to chunk
            return
            [
                new DiffChunk
                {
                    FilePath = diff.Path,
                    Content = diff.DiffText,
                    ChunkIndex = 0,
                    TotalChunks = 1,
                    StartLine = 1,
                    Context = "Full file diff",
                    DisplayName = diff.Path
                }
            ];
        }

        var lines = diff.DiffText.Split('\n');
        var chunks = new List<DiffChunk>();
        var currentChunk = new StringBuilder();
        var currentChunkStartLine = 1;
        var chunkIndex = 0;
        var lastHunkLine = 0;
        var lastHunkHeader = "Start of diff";

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineWithNewline = line + "\n";

            // Check if adding this line would exceed max size
            if (currentChunk.Length + lineWithNewline.Length > maxChunkSize && currentChunk.Length > 0)
            {
                // Try to find a good boundary to split at
                var splitPoint = FindBestSplitPoint(lines, lastHunkLine, i);

                if (splitPoint > lastHunkLine && splitPoint < i)
                {
                    // Create chunk up to split point
                    var chunkContent = BuildChunkContent(lines, currentChunkStartLine - 1, splitPoint);
                    chunks.Add(CreateChunk(diff.Path, chunkContent, chunkIndex, currentChunkStartLine, lastHunkHeader));

                    // Start new chunk from split point
                    currentChunk.Clear();
                    currentChunkStartLine = splitPoint + 1;
                    chunkIndex++;

                    // Add lines from split point to current
                    for (int j = splitPoint; j <= i; j++)
                    {
                        currentChunk.AppendLine(lines[j]);
                    }

                    lastHunkLine = i;
                    lastHunkHeader = ExtractHunkContext(line, i);
                    continue;
                }

                // No good split point found, split here but try to avoid splitting change blocks
                var adjustedSplit = AdjustSplitToAvoidChangeBlock(lines, i);
                var adjustedContent = BuildChunkContent(lines, currentChunkStartLine - 1, adjustedSplit);
                chunks.Add(CreateChunk(diff.Path, adjustedContent, chunkIndex, currentChunkStartLine, lastHunkHeader));

                currentChunk.Clear();
                currentChunkStartLine = adjustedSplit + 2; // +2 because we include one more line
                chunkIndex++;

                // Add remaining lines to new chunk
                for (int j = adjustedSplit + 1; j <= i; j++)
                {
                    currentChunk.AppendLine(lines[j]);
                }
                continue;
            }

            // Check if this line is a diff hunk header
            if (IsDiffHunkHeader(line))
            {
                lastHunkLine = i;
                lastHunkHeader = ExtractHunkContext(line, i);
            }

            currentChunk.Append(lineWithNewline);
        }

        // Add remaining chunk
        if (currentChunk.Length > 0)
        {
            chunks.Add(CreateChunk(diff.Path, currentChunk.ToString(), chunkIndex, currentChunkStartLine, lastHunkHeader));
        }

        // Update total chunks count
        var totalChunks = chunks.Count;
        foreach (var chunk in chunks)
        {
            chunk.TotalChunks = totalChunks;
            chunk.DisplayName = totalChunks > 1
                ? $"{chunk.FilePath} (chunk {chunk.ChunkIndex + 1}/{totalChunks}: {chunk.Context})"
                : chunk.FilePath;
        }

        logger.LogInformation("Split {FilePath} into {ChunkCount} chunks (original size: {OriginalSize} bytes)",
            diff.Path, totalChunks, diff.DiffText.Length);

        return chunks;
    }

    /// <summary>
    /// Finds the best point to split the diff, preferring diff hunk boundaries and empty lines.
    /// This is language-agnostic.
    /// </summary>
    private static int FindBestSplitPoint(string[] lines, int start, int end)
    {
        // Strategy 1: Look backwards from end to find a diff hunk header
        for (int i = end - 1; i > start; i--)
        {
            if (IsDiffHunkHeader(lines[i]))
            {
                return i - 1; // Split just before the hunk header
            }
        }

        // Strategy 2: Look for empty lines (common logical boundaries in any language)
        for (int i = end - 1; i > start; i--)
        {
            if (string.IsNullOrWhiteSpace(lines[i]) ||
                (lines[i].Length > 0 && string.IsNullOrWhiteSpace(lines[i].TrimStart('+', '-', ' '))))
            {
                return i;
            }
        }

        // Strategy 3: Look for lines that aren't part of a change (context lines starting with space)
        for (int i = end - 1; i > start; i--)
        {
            if (lines[i].Length > 0 && lines[i][0] == ' ')
            {
                return i;
            }
        }

        // No good boundary found, split at current position
        return end;
    }

    /// <summary>
    /// Adjusts the split point to avoid breaking in the middle of a change block.
    /// A change block is a sequence of consecutive + or - lines.
    /// </summary>
    private static int AdjustSplitToAvoidChangeBlock(string[] lines, int proposedSplit)
    {
        // If the proposed split is in the middle of a change block, move backwards
        if (proposedSplit > 0 && proposedSplit < lines.Length)
        {
            var currentLine = lines[proposedSplit];
            if (currentLine.Length > 0 && (currentLine[0] == '+' || currentLine[0] == '-'))
            {
                // Move backwards to find the start of this change block
                for (int i = proposedSplit - 1; i >= 0; i--)
                {
                    if (lines[i].Length == 0 || (lines[i][0] != '+' && lines[i][0] != '-'))
                    {
                        return i;
                    }
                }
            }
        }

        return proposedSplit;
    }

    /// <summary>
    /// Builds chunk content from an array of lines.
    /// </summary>
    private static string BuildChunkContent(string[] lines, int start, int end)
    {
        var sb = new StringBuilder();
        for (int i = start; i <= end && i < lines.Length; i++)
        {
            sb.AppendLine(lines[i]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Determines if a line is a diff hunk header.
    /// Hunk headers start with @@ and are a universal part of unified diff format.
    /// </summary>
    private static bool IsDiffHunkHeader(string line) =>
        line.TrimStart().StartsWith("@@") && DiffHunkHeaderPattern.IsMatch(line);

    /// <summary>
    /// Extracts contextual information from a diff hunk header or position.
    /// </summary>
    private static string ExtractHunkContext(string line, int lineNumber)
    {
        if (IsDiffHunkHeader(line))
        {
            // Extract line number information from hunk header
            // Format: @@ -oldstart,oldcount +newstart,newcount @@ optional context
            var match = DiffHunkHeaderPattern.Match(line);
            if (match.Success)
            {
                var newStart = match.Groups[1].Value;
                var context = line.Contains("@@", StringComparison.Ordinal) && line.LastIndexOf("@@") < line.Length - 2
                    ? line[(line.LastIndexOf("@@") + 2)..].Trim()
                    : "";

                return string.IsNullOrEmpty(context)
                    ? $"Lines starting at {newStart}"
                    : $"{context} (line {newStart})";
            }
        }

        return $"Line {lineNumber + 1}";
    }

    private static DiffChunk CreateChunk(string filePath, string content, int chunkIndex, int startLine, string context)
    {
        return new DiffChunk
        {
            FilePath = filePath,
            Content = content,
            ChunkIndex = chunkIndex,
            TotalChunks = 0, // Will be updated later
            StartLine = startLine,
            Context = context,
            DisplayName = filePath // Will be updated later
        };
    }

    [GeneratedRegex(@"@@\s+-\d+(?:,\d+)?\s+\+(\d+)(?:,\d+)?\s+@@", RegexOptions.Compiled)]
    private static partial Regex DiffHunkHeaderRegex();
}

/// <summary>
/// Represents a chunk of a diff file.
/// </summary>
public sealed class DiffChunk
{
    /// <summary>
    /// Original file path.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The diff content for this chunk.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Zero-based index of this chunk.
    /// </summary>
    public required int ChunkIndex { get; init; }

    /// <summary>
    /// Total number of chunks for this file.
    /// </summary>
    public int TotalChunks { get; set; }

    /// <summary>
    /// Starting line number of this chunk in the original file.
    /// </summary>
    public required int StartLine { get; init; }

    /// <summary>
    /// Contextual description of what this chunk contains.
    /// </summary>
    public required string Context { get; init; }

    /// <summary>
    /// Display name for this chunk (includes chunk number if file is split).
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
