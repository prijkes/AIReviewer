using OpenAI.Chat;
using NJsonSchema;
using NJsonSchema.Generation;
using Quaaly.Infrastructure.AI.FunctionParameters;
using Quaaly.Infrastructure.AzureDevOps.Functions.Parameters;
using System.Text.Json.Serialization;

namespace Quaaly.Infrastructure.AzureDevOps.Functions;

/// <summary>
/// Defines Azure DevOps functions that the AI can call to interact with pull requests.
/// These functions allow the AI to manage comments, threads, approvals, and PR metadata.
/// </summary>
public static class AzureDevOpsFunctionDefinitions
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
    /// Gets all Azure DevOps function definitions for pull request management.
    /// </summary>
    public static List<ChatTool> GetAllDefinitions()
    {
        return
        [
            // Thread and Comment Management
            CreateTool<CreateThreadParameters>(
                "create_pr_comment_thread",
                "Creates a new comment thread on a specific file and line in the pull request. Use this to point out issues or provide feedback on specific code."
            ),
            CreateTool<ReplyToThreadParameters>(
                "reply_to_thread",
                "Adds a reply to an existing comment thread. Use this to continue a conversation or provide additional information."
            ),
            CreateTool<UpdateThreadStatusParameters>(
                "update_thread_status",
                "Updates the status of a comment thread (Active, Fixed, Closed, etc.). Use this to mark issues as resolved or reopen them."
            ),
            CreateTool<GetThreadConversationParameters>(
                "get_thread_conversation",
                "Retrieves all comments in a specific thread to understand the full conversation history."
            ),

            // PR Status and Approval
            CreateTool<ApprovePullRequestParameters>(
                "approve_pull_request",
                "Approves or rejects the pull request with a specific vote. Use this after reviewing the code quality."
            ),
            CreateTool<CompletePullRequestParameters>(
                "complete_pull_request",
                "Completes (merges) the pull request. Should only be used when explicitly requested and all approvals are in place."
            ),
            CreateTool<AbandonPullRequestParameters>(
                "abandon_pull_request",
                "Abandons (closes without merging) the pull request. Use when the PR should not be merged."
            ),
            CreateTool<SetAutoCompleteParameters>(
                "set_auto_complete",
                "Enables auto-complete on the pull request, which will automatically merge when all policies are satisfied."
            ),

            // PR Management
            CreateTool<AddReviewerParameters>(
                "add_reviewer",
                "Adds a reviewer to the pull request. Can mark them as required or optional."
            ),
            CreateTool<UpdatePullRequestDescriptionParameters>(
                "update_pr_description",
                "Updates the pull request description. Use this to improve clarity or add missing information."
            ),
            CreateTool<AddPullRequestLabelParameters>(
                "add_pr_label",
                "Adds a label/tag to the pull request for categorization."
            ),

            // PR Information Retrieval
            CreateTool<GetPullRequestFilesParameters>(
                "get_pr_files",
                "Gets the list of all files changed in the pull request."
            ),
            CreateTool<GetPullRequestDiffParameters>(
                "get_pr_diff",
                "Gets the diff (changes) for a specific file in the pull request."
            ),
            CreateTool<GetCommitDetailsParameters>(
                "get_commit_details",
                "Gets detailed information about a specific commit including message, author, and changes."
            ),
            CreateTool<GetPullRequestCommitsParameters>(
                "get_pr_commits",
                "Gets all commits in the pull request."
            ),
            CreateTool<GetPullRequestWorkItemsParameters>(
                "get_pr_work_items",
                "Gets work items linked to the pull request."
            ),

            // Code Analysis Functions
            CreateTool<GetFullFileContentParameters>(
                "get_full_file_content",
                "Gets the complete content of a file from the target branch. Use this when you need to see the full context of a file beyond what's shown in the diff."
            ),
            CreateTool<GetFileAtCommitParameters>(
                "get_file_at_commit",
                "Gets the content of a file at a specific commit or branch. Useful for comparing versions or understanding what changed."
            ),
            CreateTool<SearchCodebaseParameters>(
                "search_codebase",
                "Searches the entire codebase for files containing specific text or patterns. Use this to find where functions, classes, or code patterns are defined or used."
            ),
            CreateTool<GetRelatedFilesParameters>(
                "get_related_files",
                "Finds files related to the specified file through namespace usage or imports. Use this to understand the impact of changes."
            ),
            CreateTool<GetFileHistoryParameters>(
                "get_file_history",
                "Gets the commit history for a specific file, showing how it has evolved over time."
            ),
        ];
    }

    /// <summary>
    /// Creates a ChatTool using NJsonSchema to generate the parameter schema from a C# class.
    /// </summary>
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
