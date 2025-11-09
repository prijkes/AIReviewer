using System.Text.Json.Serialization;
using AIReviewer.AzureDevOps.Models;

namespace AIReviewer.AI;

/// <summary>
/// Schema model that matches the expected AI response structure.
/// This is used for schema generation and includes all necessary JSON attributes.
/// </summary>
internal sealed record AiEnvelopeSchema(
    /// <summary>
    /// List of code review issues identified by the AI.
    /// </summary>
    [property: JsonPropertyName("issues")]
    [property: JsonRequired]
    IReadOnlyList<AiIssueSchema> Issues
);

/// <summary>
/// Schema model representing a single AI-identified issue.
/// Uses enum types to ensure structured outputs constrain values correctly.
/// </summary>
internal sealed record AiIssueSchema(
    /// <summary>
    /// Unique identifier for the issue (e.g., "PERF-001", "SEC-002").
    /// </summary>
    [property: JsonPropertyName("id")]
    [property: JsonRequired]
    string Id,

    /// <summary>
    /// Brief, descriptive title summarizing the issue.
    /// </summary>
    [property: JsonPropertyName("title")]
    [property: JsonRequired]
    string Title,

    /// <summary>
    /// Severity level: Info (informational), Warn (should be reviewed), or Error (must be fixed).
    /// </summary>
    [property: JsonPropertyName("severity")]
    [property: JsonRequired]
    IssueSeverity Severity,

    /// <summary>
    /// Issue category: Security, Correctness, Style, Performance, Docs, or Tests.
    /// </summary>
    [property: JsonPropertyName("category")]
    [property: JsonRequired]
    IssueCategory Category,

    /// <summary>
    /// File path where the issue was found (relative to repository root).
    /// </summary>
    [property: JsonPropertyName("file")]
    [property: JsonRequired]
    string File,

    /// <summary>
    /// Line number where the issue occurs (1-based indexing).
    /// </summary>
    [property: JsonPropertyName("line")]
    [property: JsonRequired]
    int Line,

    /// <summary>
    /// Detailed explanation of why this is an issue and its potential impact.
    /// </summary>
    [property: JsonPropertyName("rationale")]
    [property: JsonRequired]
    string Rationale,

    /// <summary>
    /// Actionable recommendation on how to address or fix the issue.
    /// </summary>
    [property: JsonPropertyName("recommendation")]
    [property: JsonRequired]
    string Recommendation,

    /// <summary>
    /// Code example demonstrating the recommended fix; empty if not applicable.
    /// </summary>
    [property: JsonPropertyName("fix_example")]
    [property: JsonRequired]
    string FixExample
);
