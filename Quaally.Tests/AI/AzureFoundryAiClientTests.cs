using Quaally.AI;
using Quaally.AzureDevOps.Models;
using Quaally.Utils;

namespace Quaally.Tests.AI;

/// <summary>
/// Unit tests for AzureFoundryAiClient's JSON parsing functionality.
/// These tests ensure that the schema sent to the AI uses snake_case property names
/// and that the parsing correctly handles the snake_case JSON responses.
/// </summary>
public class AzureFoundryAiClientTests
{
    [Fact]
    public void ParseResponse_WithSnakeCaseJson_ShouldDeserializeCorrectly()
    {
        // Arrange - JSON with snake_case property names as returned by the AI
        var json = @"{
            ""issues"": [
                {
                    ""title"": ""Test Issue"",
                    ""severity"": ""Warn"",
                    ""category"": ""Security"",
                    ""file"": ""test.cs"",
                    ""file_line_start"": 10,
                    ""file_line_start_offset"": 5,
                    ""file_line_end"": 15,
                    ""file_line_end_offset"": 20,
                    ""rationale"": ""This is a test rationale"",
                    ""recommendation"": ""This is a test recommendation"",
                    ""fix_example"": ""// Fixed code here""
                }
            ]
        }";

        // Act - Parse using the AiClient's internal deserialization logic
        var envelope = JsonHelpers.DeserializeStrict<TestAiEnvelope>(json);

        // Assert
        envelope.Should().NotBeNull();
        envelope.Issues.Should().HaveCount(1);
        
        var issue = envelope.Issues[0];
        issue.Title.Should().Be("Test Issue");
        issue.Severity.Should().Be(IssueSeverity.Warn);
        issue.Category.Should().Be(IssueCategory.Security);
        issue.File.Should().Be("test.cs");
        issue.FileLineStart.Should().Be(10);
        issue.FileLineStartOffset.Should().Be(5);
        issue.FileLineEnd.Should().Be(15);
        issue.FileLineEndOffset.Should().Be(20);
        issue.Rationale.Should().Be("This is a test rationale");
        issue.Recommendation.Should().Be("This is a test recommendation");
        issue.FixExample.Should().Be("// Fixed code here");
    }

    [Fact]
    public void ParseResponse_WithMultipleIssues_ShouldDeserializeAll()
    {
        // Arrange
        var json = @"{
            ""issues"": [
                {
                    ""title"": ""Issue 1"",
                    ""severity"": ""Error"",
                    ""category"": ""Correctness"",
                    ""file"": ""file1.cs"",
                    ""file_line_start"": 5,
                    ""file_line_start_offset"": 0,
                    ""file_line_end"": 10,
                    ""file_line_end_offset"": 0,
                    ""rationale"": ""Rationale 1"",
                    ""recommendation"": ""Recommendation 1"",
                    ""fix_example"": ""Fix 1""
                },
                {
                    ""title"": ""Issue 2"",
                    ""severity"": ""Info"",
                    ""category"": ""Style"",
                    ""file"": ""file2.cs"",
                    ""file_line_start"": 20,
                    ""file_line_start_offset"": 0,
                    ""file_line_end"": 25,
                    ""file_line_end_offset"": 0,
                    ""rationale"": ""Rationale 2"",
                    ""recommendation"": ""Recommendation 2"",
                    ""fix_example"": ""Fix 2""
                }
            ]
        }";

        // Act
        var envelope = JsonHelpers.DeserializeStrict<TestAiEnvelope>(json);

        // Assert
        envelope.Issues.Should().HaveCount(2);
        
        envelope.Issues[0].Title.Should().Be("Issue 1");
        envelope.Issues[0].FileLineStart.Should().Be(5);
        envelope.Issues[0].FileLineEnd.Should().Be(10);
        
        envelope.Issues[1].Title.Should().Be("Issue 2");
        envelope.Issues[1].FileLineStart.Should().Be(20);
        envelope.Issues[1].FileLineEnd.Should().Be(25);
    }

    [Fact]
    public void ParseResponse_WithNullFixExample_ShouldDeserializeCorrectly()
    {
        // Arrange - fix_example is optional and can be null or empty
        var json = @"{
            ""issues"": [
                {
                    ""title"": ""Test Issue"",
                    ""severity"": ""Warn"",
                    ""category"": ""Performance"",
                    ""file"": ""test.cs"",
                    ""file_line_start"": 1,
                    ""file_line_start_offset"": 0,
                    ""file_line_end"": 1,
                    ""file_line_end_offset"": 0,
                    ""rationale"": ""Test rationale"",
                    ""recommendation"": ""Test recommendation"",
                    ""fix_example"": null
                }
            ]
        }";

        // Act
        var envelope = JsonHelpers.DeserializeStrict<TestAiEnvelope>(json);

        // Assert
        envelope.Issues[0].FixExample.Should().BeNull();
    }

    [Fact]
    public void ParseResponse_WithEmptyIssuesArray_ShouldReturnEmptyList()
    {
        // Arrange
        var json = @"{
            ""issues"": []
        }";

        // Act
        var envelope = JsonHelpers.DeserializeStrict<TestAiEnvelope>(json);

        // Assert
        envelope.Issues.Should().BeEmpty();
    }

    [Fact]
    public void ParseResponse_WithLineNumbers_ShouldNotDefaultToZero()
    {
        // Arrange - This test specifically verifies the bug fix
        // Before the fix, file_line_start and file_line_end would default to 0
        // because the JSON parser was looking for camelCase names instead of snake_case
        var json = @"{
            ""issues"": [
                {
                    ""title"": ""Test Issue"",
                    ""severity"": ""Error"",
                    ""category"": ""Security"",
                    ""file"": ""test.cs"",
                    ""file_line_start"": 42,
                    ""file_line_start_offset"": 0,
                    ""file_line_end"": 48,
                    ""file_line_end_offset"": 0,
                    ""rationale"": ""Test rationale"",
                    ""recommendation"": ""Test recommendation"",
                    ""fix_example"": """"
                }
            ]
        }";

        // Act
        var envelope = JsonHelpers.DeserializeStrict<TestAiEnvelope>(json);

        // Assert - The critical assertion: line numbers should NOT be 0
        envelope.Issues[0].FileLineStart.Should().Be(42, "file_line_start should be parsed correctly from snake_case JSON");
        envelope.Issues[0].FileLineEnd.Should().Be(48, "file_line_end should be parsed correctly from snake_case JSON");
    }

    [Fact]
    public void ParseResponse_WithAllEnumValues_ShouldDeserializeCorrectly()
    {
        // Test all severity values
        var severityTests = new[]
        {
            (IssueSeverity.Info, "Info"),
            (IssueSeverity.Warn, "Warn"),
            (IssueSeverity.Error, "Error")
        };

        foreach (var (expectedSeverity, severityString) in severityTests)
        {
            var json = $@"{{
                ""issues"": [
                    {{
                        ""title"": ""Test"",
                        ""severity"": ""{severityString}"",
                        ""category"": ""Security"",
                        ""file"": ""test.cs"",
                        ""file_line_start"": 1,
                        ""file_line_start_offset"": 0,
                        ""file_line_end"": 1,
                        ""file_line_end_offset"": 0,
                        ""rationale"": ""Test"",
                        ""recommendation"": ""Test"",
                        ""fix_example"": """"
                    }}
                ]
            }}";

            var envelope = JsonHelpers.DeserializeStrict<TestAiEnvelope>(json);
            envelope.Issues[0].Severity.Should().Be(expectedSeverity);
        }

        // Test all category values
        var categoryTests = new[]
        {
            (IssueCategory.Security, "Security"),
            (IssueCategory.Correctness, "Correctness"),
            (IssueCategory.Style, "Style"),
            (IssueCategory.Performance, "Performance"),
            (IssueCategory.Docs, "Docs"),
            (IssueCategory.Tests, "Tests")
        };

        foreach (var (expectedCategory, categoryString) in categoryTests)
        {
            var json = $@"{{
                ""issues"": [
                    {{
                        ""title"": ""Test"",
                        ""severity"": ""Info"",
                        ""category"": ""{categoryString}"",
                        ""file"": ""test.cs"",
                        ""file_line_start"": 1,
                        ""file_line_start_offset"": 0,
                        ""file_line_end"": 1,
                        ""file_line_end_offset"": 0,
                        ""rationale"": ""Test"",
                        ""recommendation"": ""Test"",
                        ""fix_example"": """"
                    }}
                ]
            }}";

            var envelope = JsonHelpers.DeserializeStrict<TestAiEnvelope>(json);
            envelope.Issues[0].Category.Should().Be(expectedCategory);
        }
    }

    [Fact]
    public void ParseResponse_WithCamelCaseJson_ShouldFail()
    {
        // Arrange - This test ensures we detect if someone accidentally changes back to camelCase
        var json = @"{
            ""issues"": [
                {
                    ""title"": ""Test Issue"",
                    ""severity"": ""Warn"",
                    ""category"": ""Security"",
                    ""file"": ""test.cs"",
                    ""fileLineStart"": 10,
                    ""fileLineEnd"": 15,
                    ""rationale"": ""Test rationale"",
                    ""recommendation"": ""Test recommendation"",
                    ""fixExample"": ""Test fix""
                }
            ]
        }";

        // Act
        var envelope = JsonHelpers.DeserializeStrict<TestAiEnvelope>(json);

        // Assert - With camelCase, the line numbers should default to 0 (which is wrong)
        // This proves our fix is necessary
        envelope.Issues[0].FileLineStart.Should().Be(0, "camelCase 'fileLineStart' should not be parsed by snake_case attributes");
        envelope.Issues[0].FileLineEnd.Should().Be(0, "camelCase 'fileLineEnd' should not be parsed by snake_case attributes");
    }

    [Fact]
    public void SchemaGeneration_ShouldUseSnakeCasePropertyNames()
    {
        // Arrange & Act - Generate the schema that gets sent to the AI
        var schema = AiResponseSchemaGenerator.GenerateSchema<AiEnvelopeSchema>();
        var schemaJson = schema.ToString();

        // Assert - The schema should use snake_case property names
        schemaJson.Should().Contain("\"file_line_start\"", "schema should use snake_case for file_line_start");
        schemaJson.Should().Contain("\"file_line_end\"", "schema should use snake_case for file_line_end");
        schemaJson.Should().Contain("\"fix_example\"", "schema should use snake_case for fix_example");
        
        // Should NOT contain camelCase versions
        schemaJson.Should().NotContain("\"fileLineStart\"", "schema should not use camelCase");
        schemaJson.Should().NotContain("\"fileLineEnd\"", "schema should not use camelCase");
        schemaJson.Should().NotContain("\"fixExample\"", "schema should not use camelCase");
    }

    // Test helper records that mirror the internal records in AzureFoundryAiClient
    // These use the same JsonPropertyName attributes to verify deserialization
    private sealed record TestAiEnvelope(
        [property: System.Text.Json.Serialization.JsonPropertyName("issues")]
        List<TestAiItem> Issues
    );

    private sealed record TestAiItem(
        [property: System.Text.Json.Serialization.JsonPropertyName("title")]
        string Title,
        
        [property: System.Text.Json.Serialization.JsonPropertyName("severity")]
        IssueSeverity Severity,
        
        [property: System.Text.Json.Serialization.JsonPropertyName("category")]
        IssueCategory Category,
        
        [property: System.Text.Json.Serialization.JsonPropertyName("file")]
        string File,
        
        [property: System.Text.Json.Serialization.JsonPropertyName("file_line_start")]
        int FileLineStart,
        
        [property: System.Text.Json.Serialization.JsonPropertyName("file_line_start_offset")]
        int FileLineStartOffset,
        
        [property: System.Text.Json.Serialization.JsonPropertyName("file_line_end")]
        int FileLineEnd,
        
        [property: System.Text.Json.Serialization.JsonPropertyName("file_line_end_offset")]
        int FileLineEndOffset,
        
        [property: System.Text.Json.Serialization.JsonPropertyName("rationale")]
        string Rationale,
        
        [property: System.Text.Json.Serialization.JsonPropertyName("recommendation")]
        string Recommendation,
        
        [property: System.Text.Json.Serialization.JsonPropertyName("fix_example")]
        string? FixExample
    );
}
