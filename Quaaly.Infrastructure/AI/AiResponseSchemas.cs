using System.Text.Json.Serialization;
using Quaaly.Infrastructure.AzureDevOps.Models;

namespace Quaaly.Infrastructure.AI;

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
    /// Starting line number in the ACTUAL FILE (not the diff) where the issue begins. Use 1-based indexing. This represents the line number as it appears in the complete source file.
    /// </summary>
    [property: JsonPropertyName("file_line_start")]
    [property: JsonRequired]
    int FileLineStart,

    /// <summary>
    /// Character offset within the starting line where the issue begins. Use 0-based indexing. Starts at 0 for the first character of the line.
    /// </summary>
    [property: JsonPropertyName("file_line_start_offset")]
    [property: JsonRequired]
    int FileLineStartOffset,

    /// <summary>
    /// Ending line number in the ACTUAL FILE (not the diff) where the issue ends. Use 1-based indexing. For single-line issues, this should equal file_line_start. For multi-line issues, this should be the last line of the problematic code.
    /// </summary>
    [property: JsonPropertyName("file_line_end")]
    [property: JsonRequired]
    int FileLineEnd,

    /// <summary>
    /// Character offset within the ending line where the issue ends. Use 0-based indexing. For single-character issues, this should be file_line_start_offset + 1.
    /// </summary>
    [property: JsonPropertyName("file_line_end_offset")]
    [property: JsonRequired]
    int FileLineEndOffset,

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
