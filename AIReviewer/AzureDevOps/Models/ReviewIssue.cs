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
    /// <summary>Unique identifier for the issue.</summary>
    public string Id { get; init; } = string.Empty;
    /// <summary>Brief title describing the issue.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Severity level of the issue.</summary>
    public IssueSeverity Severity { get; init; }
    /// <summary>Category classification of the issue.</summary>
    public IssueCategory Category { get; init; }
    /// <summary>File path where the issue was found.</summary>
    public string FilePath { get; init; } = string.Empty;
    /// <summary>Line number where the issue occurs (0 for file-level issues).</summary>
    public int Line { get; init; }
    /// <summary>Explanation of why this is an issue.</summary>
    public string Rationale { get; init; } = string.Empty;
    /// <summary>Suggested fix or improvement.</summary>
    public string Recommendation { get; init; } = string.Empty;
    /// <summary>Optional code example showing how to fix the issue.</summary>
    public string? FixExample { get; init; }
    /// <summary>Unique fingerprint for tracking this issue across PR iterations.</summary>
    public string Fingerprint { get; init; } = string.Empty;
}
