using OpenAI.Chat;
using NJsonSchema;
using NJsonSchema.Generation;
using AIReviewer.AI.FunctionParameters;
using System.Text.Json.Serialization;

namespace AIReviewer.AI;

/// <summary>
/// Defines the functions (tools) that can be called by the AI during code review.
/// These definitions tell OpenAI what functions are available and how to call them.
/// Uses NJsonSchema to generate function parameter schemas from C# classes.
/// </summary>
public static class FunctionDefinitions
{
    private static readonly SystemTextJsonSchemaGeneratorSettings SchemaSettings = new()
    {
        SchemaType = SchemaType.JsonSchema,
        GenerateAbstractSchemas = false,
        SerializerOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }
    };

    /// <summary>
    /// Gets all available function definitions for the AI review process.
    /// </summary>
    public static List<ChatTool> GetAllDefinitions()
    {
        return
        [
            CreateTool<GetFullFileContentParameters>(
                "get_full_file_content",
                "Gets the complete content of a file from the target branch. Use this when you need to see the full context of a file beyond what's shown in the diff, such as understanding class structure, dependencies, or related methods."
            ),
            CreateTool<GetFileAtCommitParameters>(
                "get_file_at_commit",
                "Gets the content of a file at a specific commit or branch. Useful for comparing versions or understanding what changed between commits."
            ),
            CreateTool<SearchCodebaseParameters>(
                "search_codebase",
                "Searches the entire codebase for files containing specific text or patterns. Use this to find where functions, classes, interfaces, or specific code patterns are defined or used. Returns file paths, line numbers, and surrounding context."
            ),
            CreateTool<GetRelatedFilesParameters>(
                "get_related_files",
                "Finds files related to the specified file through namespace usage, imports, or being in the same namespace. Use this to understand the impact of changes and find files that might be affected by modifications."
            ),
            CreateTool<GetFileHistoryParameters>(
                "get_file_history",
                "Gets the commit history for a specific file, showing how it has evolved over time. Use this to understand the context of changes, identify patterns, or see who has worked on the file."
            )
        ];
    }

    /// <summary>
    /// Creates a ChatTool using NJsonSchema to generate the parameter schema from a C# class.
    /// </summary>
    /// <typeparam name="T">The parameter class type that defines the function's input schema.</typeparam>
    /// <param name="functionName">The name of the function.</param>
    /// <param name="description">A description of what the function does.</param>
    /// <returns>A ChatTool configured with the generated schema.</returns>
    private static ChatTool CreateTool<T>(string functionName, string description) where T : class
    {
        var schema = JsonSchema.FromType<T>(SchemaSettings);
        var schemaJson = schema.ToJson();
        
        return ChatTool.CreateFunctionTool(
            functionName: functionName,
            functionDescription: description,
            functionParameters: BinaryData.FromString(schemaJson)
        );
    }
}

/// <summary>
/// Represents a function call from the AI that needs to be executed.
/// </summary>
public sealed record FunctionCall(string FunctionName, Dictionary<string, object?> Arguments);
