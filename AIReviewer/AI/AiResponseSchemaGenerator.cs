using System.Text.Json.Serialization;
using AIReviewer.AzureDevOps.Models;
using NJsonSchema;
using NJsonSchema.Generation;

namespace AIReviewer.AI;

/// <summary>
/// Generates JSON schemas for AI response models to enable structured outputs.
/// Schemas are cached for performance.
/// </summary>
internal static class AiResponseSchemaGenerator
{
    private static BinaryData? _cachedSchema;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the JSON schema for AI review responses.
    /// The schema is generated once and cached for subsequent calls.
    /// </summary>
    /// <returns>A BinaryData containing the JSON schema.</returns>
    public static BinaryData GetResponseSchema()
    {
        if (_cachedSchema == null)
        {
            lock (_lock)
            {
                if (_cachedSchema == null)
                {
                    var settings = new SystemTextJsonSchemaGeneratorSettings
                    {
                        SchemaType = SchemaType.JsonSchema,
                        GenerateAbstractSchemas = false,
                        GenerateXmlObjects = true, // Enable XML doc comments in schema
                        SerializerOptions = new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                        }
                    };

                    var schema = JsonSchema.FromType<AiEnvelopeSchema>(settings);
                    
                    // Configure schema to be strict
                    schema.AllowAdditionalProperties = false;
                    
                    string schemaJson = schema.ToJson();
                    _cachedSchema = BinaryData.FromString(schemaJson);
                }
            }
        }

        return _cachedSchema;
    }

    /// <summary>
    /// Internal schema model that matches the expected AI response structure.
    /// This is used for schema generation and includes all necessary JSON attributes.
    /// </summary>
    private sealed record AiEnvelopeSchema(
        /// <summary>
        /// List of code review issues identified by the AI.
        /// </summary>
        [property: JsonPropertyName("issues")]
        [property: JsonRequired]
        IReadOnlyList<AiIssueSchema> Issues
    );

    /// <summary>
    /// Internal schema model representing a single AI-identified issue.
    /// Uses enum types to ensure structured outputs constrain values correctly.
    /// </summary>
    private sealed record AiIssueSchema(
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
        /// Optional code example demonstrating the recommended fix.
        /// </summary>
        [property: JsonPropertyName("fix_example")]
        string? FixExample
    );
}
