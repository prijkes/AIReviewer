using System.Text.Json;
using System.Text.Json.Serialization;
using Quaaly.Infrastructure.AI;

namespace Quaaly.Tests.AI;

public class AiResponseSchemaGeneratorTests
{
    [Fact]
    public void GenerateSchema_ShouldIncludeRequiredFields()
    {
        // Arrange & Act
        var schema = AiResponseSchemaGenerator.GenerateSchema<AiEnvelopeSchema>();
        var schemaJson = schema.ToString();
        var schemaDoc = JsonDocument.Parse(schemaJson);

        // Assert - Check root level has required for issues
        var rootRequired = schemaDoc.RootElement.GetProperty("required");
        Assert.True(rootRequired.GetArrayLength() > 0, "Root schema should have required array");

        var hasIssues = false;
        foreach (var req in rootRequired.EnumerateArray())
        {
            if (req.GetString() == "issues")
            {
                hasIssues = true;
                break;
            }
        }
        Assert.True(hasIssues, "Root schema should require 'issues' property");

        // Assert - Check AiIssueSchema definition has required fields
        var definitions = schemaDoc.RootElement.GetProperty("definitions");
        var issueSchema = definitions.GetProperty("AiIssueSchema");
        var issueRequired = issueSchema.GetProperty("required");

        // Should have all the required fields from the schema
        var requiredFields = new HashSet<string>();
        foreach (var req in issueRequired.EnumerateArray())
        {
            requiredFields.Add(req.GetString()!);
        }

        // Verify all critical fields are in the required array
        Assert.Contains("title", requiredFields);
        Assert.Contains("severity", requiredFields);
        Assert.Contains("category", requiredFields);
        Assert.Contains("file", requiredFields);
        Assert.Contains("file_line_start", requiredFields);
        Assert.Contains("file_line_start_offset", requiredFields);
        Assert.Contains("file_line_end", requiredFields);
        Assert.Contains("file_line_end_offset", requiredFields);
        Assert.Contains("rationale", requiredFields);
        Assert.Contains("recommendation", requiredFields);
        Assert.Contains("fix_example", requiredFields);
    }

    [Fact]
    public void GenerateSchema_ShouldBeCached()
    {
        // Arrange & Act
        var schema1 = AiResponseSchemaGenerator.GenerateSchema<AiEnvelopeSchema>();
        var schema2 = AiResponseSchemaGenerator.GenerateSchema<AiEnvelopeSchema>();

        // Assert - Should return the same cached instance
        Assert.Same(schema1, schema2);
    }

    [Fact]
    public void GenerateSchema_ShouldWorkWithGenericType()
    {
        // Arrange - Create a simple test type
        var schema = AiResponseSchemaGenerator.GenerateSchema<TestSchema>();
        var schemaJson = schema.ToString();
        var schemaDoc = JsonDocument.Parse(schemaJson);

        // Assert - Should have required fields
        var required = schemaDoc.RootElement.GetProperty("required");
        var requiredFields = new HashSet<string>();
        foreach (var req in required.EnumerateArray())
        {
            requiredFields.Add(req.GetString()!);
        }

        Assert.Contains("name", requiredFields);
        Assert.Contains("count", requiredFields);
    }

    [Fact]
    public void GenerateSchema_ShouldCachePerType()
    {
        // Arrange & Act
        var schema1 = AiResponseSchemaGenerator.GenerateSchema<TestSchema>();
        var schema2 = AiResponseSchemaGenerator.GenerateSchema<TestSchema>();
        var differentSchema = AiResponseSchemaGenerator.GenerateSchema<AnotherTestSchema>();

        // Assert - Same type should return same cached instance
        Assert.Same(schema1, schema2);

        // Different type should return different instance
        Assert.NotSame(schema1, differentSchema);
    }

    private sealed record TestSchema(
        [property: JsonPropertyName("name")]
        [property: JsonRequired]
        string Name,

        [property: JsonPropertyName("count")]
        [property: JsonRequired]
        int Count
    );

    private sealed record AnotherTestSchema(
        [property: JsonPropertyName("value")]
        [property: JsonRequired]
        string Value
    );
}
