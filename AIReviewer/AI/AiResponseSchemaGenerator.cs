using System.Text.Json.Serialization;
using AIReviewer.AzureDevOps.Models;
using NJsonSchema;
using NJsonSchema.Generation;
using Namotion.Reflection;

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
                        GenerateXmlObjects = false, // Disable XML metadata - not needed for OpenAI
                        AlwaysAllowAdditionalObjectProperties = false, // Ensure strict schema
                        SerializerOptions = new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                        },
                        SchemaProcessors =
                        {
                            new JsonRequiredSchemaProcessor()
                        }
                    };

                    var schema = JsonSchema.FromType<AiEnvelopeSchema>(settings);
                    
                    // Configure schema to be strict
                    schema.AllowAdditionalProperties = false;

                    // Clean up the schema to ensure OpenAI compatibility
                    CleanSchemaForOpenAI(schema);

                    string schemaJson = schema.ToJson();
                    _cachedSchema = BinaryData.FromString(schemaJson);
                }
            }
        }

        return _cachedSchema;
    }

    /// <summary>
    /// Cleans up the schema to ensure compatibility with OpenAI's structured outputs.
    /// Removes oneOf wrappers and XML metadata that NJsonSchema may add.
    /// </summary>
    private static void CleanSchemaForOpenAI(JsonSchema schema)
    {
        // Recursively clean up all definitions
        if (schema.Definitions != null)
        {
            foreach (var definition in schema.Definitions.Values)
            {
                CleanSchemaForOpenAI(definition);
            }
        }

        // Clean up properties
        if (schema.Properties != null)
        {
            foreach (var property in schema.Properties.Values)
            {
                // Remove XML metadata
                property.Xml = null;

                // If the property is an array, clean up its items
                if (property.Type == JsonObjectType.Array && property.Item != null)
                {
                    // Remove XML metadata from array items
                    property.Item.Xml = null;

                    // If items use oneOf with a single reference, unwrap it to a direct reference
                    if (property.Item.OneOf != null && property.Item.OneOf.Count == 1)
                    {
                        var singleSchema = property.Item.OneOf.First();
                        if (singleSchema.Reference != null)
                        {
                            // Replace the oneOf wrapper with a direct reference
                            property.Item = singleSchema;
                        }
                    }
                }

                // Recursively clean nested objects
                CleanSchemaForOpenAI(property);
            }
        }
    }

    /// <summary>
    /// Schema processor that ensures JsonRequired attributes are properly reflected in the schema's required array.
    /// This is necessary because NJsonSchema doesn't automatically populate the required array for System.Text.Json.Serialization.JsonRequiredAttribute.
    /// OpenAI's structured output requires that all required properties are explicitly listed in the 'required' array.
    /// </summary>
    private class JsonRequiredSchemaProcessor : ISchemaProcessor
    {
        public void Process(SchemaProcessorContext context)
        {
            // Process each property in the current type
            foreach (var property in context.ContextualType.Properties)
            {
                // Only process properties declared in this type (not inherited)
                if (property.PropertyInfo.DeclaringType != context.ContextualType.Type)
                {
                    continue;
                }

                // Check if property has JsonRequired attribute or C# required modifier
                var hasJsonRequired = property.GetAttribute<JsonRequiredAttribute>(inherit: false) != null;
                var hasRequiredModifier = property.GetAttribute<System.Runtime.CompilerServices.RequiredMemberAttribute>(inherit: false) != null;
                
                if (hasJsonRequired || hasRequiredModifier)
                {
                    // Get the JSON property name (respecting JsonPropertyName attribute)
                    var jsonPropertyNameAttr = property.GetAttribute<JsonPropertyNameAttribute>(inherit: false);
                    var jsonPropertyName = jsonPropertyNameAttr?.Name ?? property.Name;
                    
                    // Find the corresponding schema property by the JSON name
                    var schemaProperty = context.Schema.Properties
                        .FirstOrDefault(x => x.Key.Equals(jsonPropertyName, StringComparison.OrdinalIgnoreCase));
                    
                    if (schemaProperty.Value != null)
                    {
                        // Mark the property as required in the schema
                        schemaProperty.Value.IsRequired = true;
                    }
                }
            }
        }
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
        /// Code example demonstrating the recommended fix; empty if not applicable.
        /// </summary>
        [property: JsonPropertyName("fix_example")]
        [property: JsonRequired]
        string FixExample
    );
}
