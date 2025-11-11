namespace AIReviewer.AzureDevOps.Models;

/// <summary>
/// Severity levels for review issues.
/// </summary>
public enum IssueSeverity
{
    /// <summary>Informational issue - for reference only.</summary>
    Info,
    /// <summary>Warning - should be reviewed but doesn't block approval.</summary>
    Warn,
    /// <summary>Error - blocks approval until resolved.</summary>
    Error
}

/// <summary>
/// Categories for classifying review issues.
/// </summary>
public enum IssueCategory
{
    /// <summary>Security-related concerns.</summary>
    Security,
    /// <summary>Logical or functional correctness issues.</summary>
    Correctness,
    /// <summary>Code style and formatting issues.</summary>
    Style,
    /// <summary>Performance-related concerns.</summary>
    Performance,
    /// <summary>Documentation issues.</summary>
    Docs,
    /// <summary>Testing-related issues.</summary>
    Tests
}

/// <summary>
/// Represents a single code review issue identified by the AI reviewer.
/// </summary>
public sealed class ReviewIssue
{
    /// <summary>Brief title describing the issue.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Severity level of the issue.</summary>
    public IssueSeverity Severity { get; init; }
    /// <summary>Category classification of the issue.</summary>
    public IssueCategory Category { get; init; }
    /// <summary>File path where the issue was found.</summary>
    public string FilePath { get; init; } = string.Empty;
    /// <summary>Starting line number in the actual file where the issue begins (0 for file-level issues).</summary>
    public int LineStart { get; init; }
    /// <summary>Character offset within the starting line where the issue begins (0-based indexing).</summary>
    public int LineStartOffset { get; init; }
    /// <summary>Ending line number in the actual file where the issue ends (0 for file-level issues).</summary>
    public int LineEnd { get; init; }
    /// <summary>Character offset within the ending line where the issue ends (0-based indexing).</summary>
    public int LineEndOffset { get; init; }
    /// <summary>Explanation of why this is an issue.</summary>
    public string Rationale { get; init; } = string.Empty;
    /// <summary>Suggested fix or improvement.</summary>
    public string Recommendation { get; init; } = string.Empty;
    /// <summary>Optional code example showing how to fix the issue.</summary>
    public string? FixExample { get; init; }
    /// <summary>Unique fingerprint for tracking this issue across PR iterations.</summary>
    public string Fingerprint { get; init; } = string.Empty;
    /// <summary>Indicates whether this issue is for a deleted file (comments should appear on left side of diff).</summary>
    public bool IsDeleted { get; init; }
}
