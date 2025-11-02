using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace AIReviewer.Diff;

/// <summary>
/// Intelligently splits large diffs into smaller, logical chunks for review.
/// </summary>
public sealed class DiffChunker
{
    private readonly ILogger<DiffChunker> _logger;

    // Patterns to identify code boundaries
    private static readonly Regex ClassPattern = new(@"^\+?\s*(public|private|internal|protected)?\s*(static\s+)?(class|interface|struct|enum)\s+\w+", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex MethodPattern = new(@"^\+?\s*(public|private|internal|protected)?\s*(static\s+)?(async\s+)?[\w<>]+\s+\w+\s*\([^)]*\)", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex PropertyPattern = new(@"^\+?\s*(public|private|internal|protected)?\s*(static\s+)?[\w<>]+\s+\w+\s*\{\s*(get|set)", RegexOptions.Compiled | RegexOptions.Multiline);

    public DiffChunker(ILogger<DiffChunker> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Splits a large diff into logical chunks based on code structure.
    /// </summary>
    /// <param name="diff">The diff to chunk.</param>
    /// <param name="maxChunkSize">Maximum size of each chunk in bytes.</param>
    /// <returns>List of diff chunks with contextual information.</returns>
    public List<DiffChunk> ChunkDiff(ReviewFileDiff diff, int maxChunkSize)
    {
        if (diff.DiffText.Length <= maxChunkSize)
        {
            // No need to chunk
            return new List<DiffChunk>
            {
                new DiffChunk
                {
                    FilePath = diff.Path,
                    Content = diff.DiffText,
                    ChunkIndex = 0,
                    TotalChunks = 1,
                    StartLine = 1,
                    Context = "Full file diff"
                }
            };
        }

        var lines = diff.DiffText.Split('\n');
        var chunks = new List<DiffChunk>();
        var currentChunk = new StringBuilder();
        var currentChunkStartLine = 1;
        var chunkIndex = 0;
        var lastBoundaryLine = 0;
        var lastBoundaryContext = "Start of file";

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineWithNewline = line + "\n";

            // Check if adding this line would exceed max size
            if (currentChunk.Length + lineWithNewline.Length > maxChunkSize && currentChunk.Length > 0)
            {
                // Try to find a good boundary to split at
                var splitPoint = FindBestSplitPoint(lines, lastBoundaryLine, i);
                
                if (splitPoint > lastBoundaryLine && splitPoint < i)
                {
                    // Create chunk up to split point
                    chunks.Add(CreateChunk(diff.Path, currentChunk.ToString(), chunkIndex, currentChunkStartLine, lastBoundaryContext));
                    
                    // Start new chunk from split point
                    currentChunk.Clear();
                    currentChunkStartLine = splitPoint + 1;
                    chunkIndex++;
                    
                    // Add lines from split point to current
                    for (int j = splitPoint; j <= i; j++)
                    {
                        currentChunk.AppendLine(lines[j]);
                    }
                    
                    lastBoundaryLine = i;
                    lastBoundaryContext = ExtractContext(line);
                    continue;
                }
                
                // No good split point found, split here
                chunks.Add(CreateChunk(diff.Path, currentChunk.ToString(), chunkIndex, currentChunkStartLine, lastBoundaryContext));
                currentChunk.Clear();
                currentChunkStartLine = i + 1;
                chunkIndex++;
            }

            // Check if this line is a code boundary
            if (IsCodeBoundary(line))
            {
                lastBoundaryLine = i;
                lastBoundaryContext = ExtractContext(line);
            }

            currentChunk.Append(lineWithNewline);
        }

        // Add remaining chunk
        if (currentChunk.Length > 0)
        {
            chunks.Add(CreateChunk(diff.Path, currentChunk.ToString(), chunkIndex, currentChunkStartLine, lastBoundaryContext));
        }

        // Update total chunks count
        var totalChunks = chunks.Count;
        foreach (var chunk in chunks)
        {
            chunk.TotalChunks = totalChunks;
        }

        _logger.LogInformation("Split {FilePath} into {ChunkCount} chunks (original size: {OriginalSize} bytes)", 
            diff.Path, totalChunks, diff.DiffText.Length);

        return chunks;
    }

    /// <summary>
    /// Finds the best point to split the diff, preferring code boundaries.
    /// </summary>
    private int FindBestSplitPoint(string[] lines, int start, int end)
    {
        // Look backwards from end to find a code boundary
        for (int i = end - 1; i > start; i--)
        {
            if (IsCodeBoundary(lines[i]))
            {
                return i;
            }
        }

        // No boundary found, split at current position
        return end;
    }

    /// <summary>
    /// Determines if a line represents a code boundary (class, method, property, etc.).
    /// </summary>
    private bool IsCodeBoundary(string line)
    {
        return ClassPattern.IsMatch(line) || 
               MethodPattern.IsMatch(line) || 
               PropertyPattern.IsMatch(line);
    }

    /// <summary>
    /// Extracts contextual information from a code boundary line.
    /// </summary>
    private string ExtractContext(string line)
    {
        line = line.TrimStart('+', '-', ' ', '\t');
        
        if (ClassPattern.IsMatch(line))
        {
            var match = Regex.Match(line, @"(class|interface|struct|enum)\s+(\w+)");
            if (match.Success)
                return $"{match.Groups[1].Value} {match.Groups[2].Value}";
        }
        
        if (MethodPattern.IsMatch(line))
        {
            var match = Regex.Match(line, @"([\w<>]+)\s+(\w+)\s*\(");
            if (match.Success)
                return $"Method {match.Groups[2].Value}";
        }
        
        if (PropertyPattern.IsMatch(line))
        {
            var match = Regex.Match(line, @"([\w<>]+)\s+(\w+)\s*\{");
            if (match.Success)
                return $"Property {match.Groups[2].Value}";
        }

        return "Code section";
    }

    private DiffChunk CreateChunk(string filePath, string content, int chunkIndex, int startLine, string context)
    {
        return new DiffChunk
        {
            FilePath = filePath,
            Content = content,
            ChunkIndex = chunkIndex,
            TotalChunks = 0, // Will be updated later
            StartLine = startLine,
            Context = context
        };
    }
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
    /// Gets a display name for this chunk.
    /// </summary>
    public string DisplayName => TotalChunks > 1 
        ? $"{FilePath} (chunk {ChunkIndex + 1}/{TotalChunks}: {Context})" 
        : FilePath;
}
