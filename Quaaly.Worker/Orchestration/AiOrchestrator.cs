using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using OpenAI.Chat;
using Quaaly.Infrastructure.AI;
using Quaaly.Infrastructure.AzureDevOps;
using Quaaly.Infrastructure.AzureDevOps.Functions;
using Quaaly.Infrastructure.Options;
using Quaaly.Worker.Queue.Models;
using System.Text;

namespace Quaaly.Worker.Orchestration;

/// <summary>
/// Orchestrates AI interactions for pull request comments.
/// Handles conversational context, function calling, and response generation.
/// </summary>
public sealed class AiOrchestrator(
    ILogger<AiOrchestrator> logger,
    ChatClient chatClient,
    IAdoSdkClient adoClient,
    AzureDevOpsFunctionExecutor functionExecutor,
    ReviewContextRetriever contextRetriever,
    BotIdentityService botIdentity,
    MentionDetector mentionDetector,
    IOptionsMonitor<ReviewerOptions> options)
{
    private readonly ILogger<AiOrchestrator> _logger = logger;
    private readonly ChatClient _chatClient = chatClient;
    private readonly IAdoSdkClient _adoClient = adoClient;
    private readonly AzureDevOpsFunctionExecutor _functionExecutor = functionExecutor;
    private readonly ReviewContextRetriever _contextRetriever = contextRetriever;
    private readonly BotIdentityService _botIdentity = botIdentity;
    private readonly MentionDetector _mentionDetector = mentionDetector;
    private readonly ReviewerOptions _options = options.CurrentValue;

    /// <summary>
    /// Processes a pull request comment event where the bot was mentioned.
    /// </summary>
    public async Task ProcessCommentEventAsync(
        PullRequestCommentEventResource commentEvent,
        CancellationToken cancellationToken = default)
    {
        if (commentEvent?.PullRequest == null || commentEvent.Comment == null)
        {
            _logger.LogWarning("Invalid comment event - missing PR or comment data");
            return;
        }

        var pr = commentEvent.PullRequest;
        var comment = commentEvent.Comment;

        _logger.LogInformation(
            "Processing comment event for PR {PrId} in repo {RepoName}",
            pr.PullRequestId,
            pr.Repository?.Name ?? "Unknown");

        // Check if bot was mentioned
        if (!_mentionDetector.IsBotMentioned(comment.Content))
        {
            _logger.LogDebug("Bot not mentioned in comment, skipping");
            return;
        }

        // Ensure repository is not null
        if (pr.Repository == null)
        {
            _logger.LogWarning("Invalid comment event - missing repository data");
            return;
        }

        // Set function executor context
        _functionExecutor.SetContext(pr.Repository.Id, pr.PullRequestId);

        // Set context retriever context for code analysis functions
        var prContext = new Infrastructure.AzureDevOps.Models.PullRequestContext(
            pr,
            pr.Repository,
            [],
            null!);
        _contextRetriever.SetContext(prContext);

        // Get user request after mention
        var userRequest = _mentionDetector.GetTextAfterMention(comment.Content);
        _logger.LogInformation("User request: {Request}", userRequest);

        // Build conversation context
        var messages = await BuildConversationContextAsync(pr, comment, userRequest, cancellationToken);

        // Get function definitions
        var tools = AzureDevOpsFunctionDefinitions.GetAllDefinitions();

        // Execute AI conversation with function calling
        await ExecuteAiConversationAsync(pr, comment, messages, tools, cancellationToken);
    }

    /// <summary>
    /// Builds the conversation context including PR info, thread history, and user request.
    /// </summary>
    private async Task<List<ChatMessage>> BuildConversationContextAsync(
        GitPullRequest pr,
        CommentInfo comment,
        string userRequest,
        CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>();

        // System message with PR context
        var systemMessage = BuildSystemMessage(pr);
        messages.Add(ChatMessage.CreateSystemMessage(systemMessage));

        // Add thread conversation history if this is a reply
        if (comment.ParentCommentId.HasValue)
        {
            var threadHistory = await GetThreadHistoryAsync(pr, comment, cancellationToken);
            if (!string.IsNullOrEmpty(threadHistory))
            {
                messages.Add(ChatMessage.CreateSystemMessage($"Conversation history:\n{threadHistory}"));
            }
        }

        // Add user request
        messages.Add(ChatMessage.CreateUserMessage(userRequest));

        return messages;
    }

    /// <summary>
    /// Builds the system message with PR context and available functions.
    /// </summary>
    private static string BuildSystemMessage(GitPullRequest pr)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an AI code reviewer assistant integrated into Azure DevOps pull requests.");
        sb.AppendLine("You can help with code reviews, answer questions, and manage pull request workflow.");
        sb.AppendLine();
        sb.AppendLine("## Current Pull Request Context");
        sb.AppendLine($"- **Title**: {pr.Title}");
        sb.AppendLine($"- **Description**: {pr.Description ?? "No description"}");
        sb.AppendLine($"- **Author**: {pr.CreatedBy?.DisplayName}");
        sb.AppendLine($"- **Source Branch**: {pr.SourceRefName?.Replace("refs/heads/", "")}");
        sb.AppendLine($"- **Target Branch**: {pr.TargetRefName?.Replace("refs/heads/", "")}");
        sb.AppendLine($"- **Status**: {pr.Status}");
        sb.AppendLine();

        sb.AppendLine("## Available Functions");
        sb.AppendLine("You have access to various functions to interact with the pull request:");
        sb.AppendLine("- Create comment threads on specific files/lines");
        sb.AppendLine("- Reply to existing threads");
        sb.AppendLine("- Update thread status (e.g., mark as fixed, closed)");
        sb.AppendLine("- Approve or reject the pull request");
        sb.AppendLine("- Get PR files, commits, and work items");
        sb.AppendLine("- Search codebase and analyze specific files");
        sb.AppendLine("- And more...");
        sb.AppendLine();

        sb.AppendLine("## Instructions");
        sb.AppendLine("- Be helpful, concise, and professional");
        sb.AppendLine("- Use functions when needed to accomplish tasks");
        sb.AppendLine("- For code reviews, use create_pr_comment_thread to add inline comments");
        sb.AppendLine("- When asked to review, analyze the code and provide constructive feedback");
        sb.AppendLine("- Always explain your reasoning when making suggestions");
        sb.AppendLine("- You can use get_pr_files to see what files changed");
        sb.AppendLine("- You can use get_full_file_content to examine specific files");
        sb.AppendLine("- Use search_codebase to find related code patterns");

        return sb.ToString();
    }

    /// <summary>
    /// Gets the conversation history from a thread.
    /// </summary>
    private async Task<string> GetThreadHistoryAsync(
        GitPullRequest pr,
        CommentInfo comment,
        CancellationToken cancellationToken)
    {
        try
        {
            // Find the thread containing this comment
            var threads = await _adoClient.Git.GetThreadsAsync(
                pr.Repository.Id,
                pr.PullRequestId,
                cancellationToken: cancellationToken);

            var thread = threads.FirstOrDefault(t => 
                t.Comments?.Any(c => c.Id == comment.Id) == true);

            if (thread?.Comments == null || thread.Comments.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var botId = await _botIdentity.GetBotUserIdAsync(cancellationToken);

            foreach (var c in thread.Comments.OrderBy(c => c.PublishedDate))
            {
                var isBot = c.Author?.Id.ToString() == botId;
                var role = isBot ? "Assistant" : c.Author?.DisplayName ?? "User";
                sb.AppendLine($"[{role}]: {c.Content}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve thread history");
            return string.Empty;
        }
    }

    /// <summary>
    /// Executes the AI conversation with function calling support.
    /// </summary>
    private async Task ExecuteAiConversationAsync(
        GitPullRequest pr,
        CommentInfo comment,
        List<ChatMessage> messages,
        List<ChatTool> tools,
        CancellationToken cancellationToken)
    {
        var iteration = 0;
        var finalResponse = new StringBuilder();

        while (iteration < _options.FunctionCalling.MaxConversationIterations)
        {
            iteration++;
            _logger.LogDebug("AI conversation iteration {Iteration}", iteration);

            // Call AI with function definitions
            var chatOptions = new ChatCompletionOptions();
            foreach (var tool in tools)
            {
                chatOptions.Tools.Add(tool);
            }

            ChatCompletion completion;
            try
            {
                completion = await _chatClient.CompleteChatAsync(messages, chatOptions, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling AI");
                await ReplyToCommentAsync(pr, comment, $"Sorry, I encountered an error: {ex.Message}", cancellationToken);
                return;
            }

            var assistantMessage = completion.Content[0];
            
            // Convert ChatMessageContentPart to ChatMessage format
            var contentParts = new List<ChatMessageContentPart>();
            foreach (var part in completion.Content)
            {
                contentParts.Add(part);
            }
            messages.Add(new AssistantChatMessage(contentParts));

            // Check if AI wants to call functions
            if (completion.FinishReason == ChatFinishReason.ToolCalls && completion.ToolCalls.Count > 0)
            {
                _logger.LogInformation("AI requested {Count} function calls", completion.ToolCalls.Count);

                foreach (var toolCall in completion.ToolCalls)
                {
                    var functionName = toolCall.FunctionName;
                    var functionArgs = toolCall.FunctionArguments.ToString();

                    _logger.LogInformation("Executing function: {FunctionName}", functionName);

                    // Execute the function
                    var result = await _functionExecutor.ExecuteFunctionAsync(
                        functionName,
                        functionArgs,
                        cancellationToken);

                    _logger.LogDebug("Function {FunctionName} result: {Result}", functionName, result);

                    // Add function result to conversation
                    messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, result));
                }

                // Continue the loop to get AI's response after function execution
                continue;
            }

            // AI has finished - collect final response
            if (assistantMessage != null && !string.IsNullOrEmpty(assistantMessage.Text))
            {
                finalResponse.AppendLine(assistantMessage.Text);
            }

            // Check if conversation is complete
            if (completion.FinishReason == ChatFinishReason.Stop || 
                completion.FinishReason == ChatFinishReason.Length)
            {
                break;
            }
        }

        // Send final response as a comment reply
        var responseText = finalResponse.ToString().Trim();
        if (!string.IsNullOrEmpty(responseText))
        {
            await ReplyToCommentAsync(pr, comment, responseText, cancellationToken);
        }
        else
        {
            _logger.LogWarning("No final response generated from AI conversation");
        }
    }

    /// <summary>
    /// Replies to a comment in the pull request.
    /// </summary>
    private async Task ReplyToCommentAsync(
        GitPullRequest pr,
        CommentInfo comment,
        string responseText,
        CancellationToken cancellationToken)
    {
        try
        {
            // Find the thread containing this comment
            var threads = await _adoClient.Git.GetThreadsAsync(
                pr.Repository.Id,
                pr.PullRequestId,
                cancellationToken: cancellationToken);

            var thread = threads.FirstOrDefault(t => 
                t.Comments?.Any(c => c.Id == comment.Id) == true);

            if (thread == null)
            {
                _logger.LogWarning("Could not find thread for comment {CommentId}", comment.Id);
                return;
            }

            // Add reply to thread
            var reply = new Comment
            {
                Content = responseText,
                CommentType = CommentType.Text
            };

            await _adoClient.Git.CreateCommentAsync(
                reply,
                pr.Repository.Id,
                pr.PullRequestId,
                thread.Id,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully replied to comment in thread {ThreadId}", thread.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reply to comment");
        }
    }
}
