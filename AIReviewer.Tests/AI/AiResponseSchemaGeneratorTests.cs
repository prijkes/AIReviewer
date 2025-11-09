using System.Text.Json;
using AIReviewer.AI;

namespace AIReviewer.Tests.AI;

public class AiResponseSchemaGeneratorTests
{
    [Fact]
    public void GetResponseSchema_ShouldIncludeRequiredFields()
    {
        // Arrange & Act
        var schema = AiResponseSchemaGenerator.GetResponseSchema();
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
        Assert.Contains("id", requiredFields);
        Assert.Contains("title", requiredFields);
        Assert.Contains("severity", requiredFields);
        Assert.Contains("category", requiredFields);
        Assert.Contains("file", requiredFields);
        Assert.Contains("line", requiredFields);
        Assert.Contains("rationale", requiredFields);
        Assert.Contains("recommendation", requiredFields);
        Assert.Contains("fix_example", requiredFields);
    }
    
    [Fact]
    public void GetResponseSchema_ShouldBeCached()
    {
        // Arrange & Act
        var schema1 = AiResponseSchemaGenerator.GetResponseSchema();
        var schema2 = AiResponseSchemaGenerator.GetResponseSchema();
        
        // Assert - Should return the same cached instance
        Assert.Same(schema1, schema2);
    }
}
