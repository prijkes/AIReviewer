namespace AIReviewer.AzureDevOps.Models;

public enum IssueSeverity
{
    Info,
    Warn,
    Error
}

public enum IssueCategory
{
    Security,
    Correctness,
    Style,
    Performance,
    Docs,
    Tests
}

public sealed class ReviewIssue
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public IssueSeverity Severity { get; init; }
    public IssueCategory Category { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public int Line { get; init; }
    public string Rationale { get; init; } = string.Empty;
    public string Recommendation { get; init; } = string.Empty;
    public string? FixExample { get; init; }
    public string Fingerprint { get; init; } = string.Empty;
}
