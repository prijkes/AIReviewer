using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quaally.Infrastructure.Utils;
using Quaally.Infrastructure.Diff;
using Quaally.Infrastructure.Options;
using Quaally.Infrastructure.Review;
using Quaally.Infrastructure.AzureDevOps.Models;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Quaally.Infrastructure.AI;

/// <summary>
/// Azure OpenAI client implementation for AI-powered code reviews.
/// Sends code diffs and PR metadata to Azure OpenAI for analysis and returns structured issues.
/// </summary>
public sealed class AzureFoundryAiClient : IAiClient
{
    private readonly ILogger<AzureFoundryAiClient> _logger;
    private readonly ReviewerOptions _options;
    private readonly AzureOpenAIClient _client;
    private readonly ReviewContextRetriever _contextRetriever;
    private readonly PromptBuilder _promptBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureFoundryAiClient"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="options">Configuration options for the reviewer.</param>
    /// <param name="contextRetriever">Context retriever for function calling.</param>
    /// <param name="promptBuilder">Prompt builder for constructing review prompts.</param>
    public AzureFoundryAiClient(
        ILogger<AzureFoundryAiClient> logger,
        IOptionsMonitor<ReviewerOptions> options,
        ReviewContextRetriever contextRetriever,
        PromptBuilder promptBuilder)
    {
        _logger = logger;
        _options = options.CurrentValue;
        _contextRetriever = contextRetriever;
        _promptBuilder = promptBuilder;

        _client = new AzureOpenAIClient(
            new Uri(_options.AiFoundryEndpoint),
            new ApiKeyCredential(_options.AiFoundryApiKey)
        );
    }

    /// <inheritdoc/>
    public async Task<AiReviewResponse> ReviewAsync(string policy, ReviewFileDiff fileDiff, string language, ProgrammingLanguageDetector.ProgrammingLanguage programmingLanguage, List<ExistingComment> existingComments, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Requesting AI review for {Path} ({DiffSize} bytes, {CommentCount} existing comments) in language: {Language}, programming language: {ProgrammingLanguage}",
            fileDiff.Path, fileDiff.DiffText.Length, existingComments.Count, language, ProgrammingLanguageDetector.GetDisplayName(programmingLanguage));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Use ChatClient for structured outputs support
        var chatClient = _client.GetChatClient(_options.AiFoundryDeployment);

        var systemPrompt = await _promptBuilder.BuildFileReviewSystemPromptAsync(policy, language, programmingLanguage, cancellationToken);
        var userPrompt = await _promptBuilder.BuildFileReviewUserPromptAsync(fileDiff, existingComments, cancellationToken);

        // Log the prompts being sent to the AI
        _logger.LogInformation("[AI-SYSTEM-PROMPT] System prompt for {Path}:\n{SystemPrompt}", fileDiff.Path, systemPrompt);
        _logger.LogInformation("[AI-USER-PROMPT] User prompt for {Path}:\n{UserPrompt}", fileDiff.Path, userPrompt);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var schema = AiResponseSchemaGenerator.GenerateSchema<AiEnvelopeSchema>();
        
        // Log the schema being sent to the AI
        _logger.LogInformation("[AI-SCHEMA] JSON schema sent to AI:\n{Schema}", schema.ToString());
        
        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "ai_review_response",
                jsonSchema: schema,
                jsonSchemaIsStrict: true)
        };

        // Add function calling tools if enabled
        if (_options.EnableFunctionCalling)
        {
            foreach (var tool in FunctionDefinitions.GetAllDefinitions())
            {
                options.Tools.Add(tool);
            }
        }

        var completion = await chatClient.CompleteChatAsync(messages, options, cancellationToken);

        // Handle function calls if present
        if (_options.EnableFunctionCalling)
        {
            completion = await HandleFunctionCallsAsync(chatClient, messages, options, completion, cancellationToken);
        }

        // Check for refusal - model declined to respond
        if (completion.Value.FinishReason == ChatFinishReason.ContentFilter)
        {
            _logger.LogWarning("[AI-REFUSAL] Model refused to respond for {Path} due to content filter. Returning empty results.", fileDiff.Path);
            return new AiReviewResponse([]);
        }

        // Check if we have content to parse
        if (completion.Value.Content.Count == 0 || string.IsNullOrEmpty(completion.Value.Content[0].Text))
        {
            _logger.LogWarning("[AI-EMPTY-RESPONSE] Model returned empty response for {Path}. Returning empty results.", fileDiff.Path);
            return new AiReviewResponse([]);
        }

        var content = completion.Value.Content[0].Text;

        stopwatch.Stop();
        
        // Log raw JSON response for debugging line number issues
        _logger.LogInformation("[AI-JSON-RESPONSE] Raw AI response for {Path}:\n{JsonResponse}", fileDiff.Path, content);
        
        var response = ParseResponse(content);

        _logger.LogInformation("AI reviewed {Path}: {IssueCount} issues found in {Ms}ms (response: {Size} bytes)",
            fileDiff.Path, response.Issues.Count, stopwatch.ElapsedMilliseconds, content.Length);

        // Log token usage if available
        if (completion.Value.Usage != null)
        {
            _logger.LogInformation("Token usage - Input: {InputTokens}, Output: {OutputTokens}, Total: {TotalTokens}",
                completion.Value.Usage.InputTokenCount,
                completion.Value.Usage.OutputTokenCount,
                completion.Value.Usage.TotalTokenCount);
        }

        return response;
    }

    /// <inheritdoc/>
    public async Task<AiReviewResponse> ReviewPullRequestMetadataAsync(string policy, PullRequestMetadata metadata, string language, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Requesting metadata review in language: {Language}", language);

        // Use ChatClient for structured outputs support
        var chatClient = _client.GetChatClient(_options.AiFoundryDeployment);

        var systemPrompt = await _promptBuilder.BuildMetadataReviewSystemPromptAsync(policy, language, cancellationToken);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(_promptBuilder.BuildMetadataReviewUserPrompt(metadata))
        };

        var schema = AiResponseSchemaGenerator.GenerateSchema<AiEnvelopeSchema>();

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "ai_review_response",
                jsonSchema: schema,
                jsonSchemaIsStrict: true)
        };

        var completion = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        
        // Check for refusal - model declined to respond
        if (completion.Value.FinishReason == ChatFinishReason.ContentFilter)
        {
            _logger.LogWarning("[AI-REFUSAL] Model refused to respond for metadata review due to content filter. Returning empty results.");
            return new AiReviewResponse([]);
        }

        // Check if we have content to parse
        if (completion.Value.Content.Count == 0 || string.IsNullOrEmpty(completion.Value.Content[0].Text))
        {
            _logger.LogWarning("[AI-EMPTY-RESPONSE] Model returned empty response for metadata review. Returning empty results.");
            return new AiReviewResponse([]);
        }

        var content = completion.Value.Content[0].Text;

        return ParseResponse(content);
    }

    /// <summary>
    /// Parses the raw JSON response from the AI model into a structured review response.
    /// With structured outputs, the schema enforces enum values and required fields.
    /// </summary>
    /// <param name="content">The raw JSON content from the AI model.</param>
    /// <returns>A structured <see cref="AiReviewResponse"/> containing the identified issues.</returns>
    /// <exception cref="InvalidDataException">Thrown when JSON parsing fails.</exception>
    private static AiReviewResponse ParseResponse(string content)
    {
        var envelope = JsonHelpers.DeserializeStrict<AiEnvelope>(content);

        // With structured outputs, enum values and required fields are guaranteed by the schema
        var issues = envelope.Issues.Select(issue => new AiIssue(
            issue.Title,
            issue.Severity,
            issue.Category,
            issue.File,
            issue.FileLineStart,
            issue.FileLineStartOffset,
            issue.FileLineEnd,
            issue.FileLineEndOffset,
            issue.Rationale,
            issue.Recommendation,
            issue.FixExample
        )).ToList();

        return new AiReviewResponse(issues);
    }

    /// <summary>
    /// Internal envelope record for deserializing the AI response JSON.
    /// With structured outputs, all required fields are guaranteed to be non-null.
    /// Uses JsonPropertyName attributes to match the snake_case schema sent to the AI.
    /// </summary>
    /// <param name="Issues">List of issue items from the response.</param>
    private sealed record AiEnvelope(
        [property: JsonPropertyName("issues")]
        List<AiItem> Issues
    );

    /// <summary>
    /// Internal record representing a single issue item in the AI response JSON.
    /// With structured outputs, enum values and required fields are guaranteed by the schema.
    /// Uses JsonPropertyName attributes to match the snake_case schema sent to the AI.
    /// </summary>
    private sealed record AiItem(
        [property: JsonPropertyName("title")]
        string Title,
        
        [property: JsonPropertyName("severity")]
        IssueSeverity Severity,
        
        [property: JsonPropertyName("category")]
        IssueCategory Category,
        
        [property: JsonPropertyName("file")]
        string File,
        
        [property: JsonPropertyName("file_line_start")]
        int FileLineStart,
        
        [property: JsonPropertyName("file_line_start_offset")]
        int FileLineStartOffset,
        
        [property: JsonPropertyName("file_line_end")]
        int FileLineEnd,
        
        [property: JsonPropertyName("file_line_end_offset")]
        int FileLineEndOffset,
        
        [property: JsonPropertyName("rationale")]
        string Rationale,
        
        [property: JsonPropertyName("recommendation")]
        string Recommendation,
        
        [property: JsonPropertyName("fix_example")]
        string? FixExample
    );

    /// <summary>
    /// Handles function calls from the AI, executing them and continuing the conversation.
    /// </summary>
    private async Task<ClientResult<ChatCompletion>> HandleFunctionCallsAsync(
        ChatClient chatClient,
        List<ChatMessage> messages,
        ChatCompletionOptions options,
        ClientResult<ChatCompletion> completion,
        CancellationToken cancellationToken)
    {
        const int MaxFunctionCalls = 5;
        int functionCallCount = 0;

        while (completion.Value.FinishReason == ChatFinishReason.ToolCalls && functionCallCount < MaxFunctionCalls)
        {
            functionCallCount++;
            _logger.LogDebug("AI requested function calls (iteration {Count}/{Max})", functionCallCount, MaxFunctionCalls);

            // Add assistant's response with tool calls to messages
            messages.Add(new AssistantChatMessage(completion.Value));

            // Execute each tool call
            foreach (var toolCall in completion.Value.ToolCalls)
            {
                if (toolCall.Kind != ChatToolCallKind.Function)
                {
                    _logger.LogWarning("Unsupported tool call kind: {Kind}", toolCall.Kind);
                    continue;
                }

                var functionName = toolCall.FunctionName;
                var functionArgs = toolCall.FunctionArguments;

                _logger.LogInformation("Executing function: {FunctionName} with args: {Args}", functionName, functionArgs);

                try
                {
                    var result = await ExecuteFunctionAsync(functionName, functionArgs.ToString(), cancellationToken);
                    messages.Add(new ToolChatMessage(toolCall.Id, result));
                    _logger.LogDebug("Function {FunctionName} returned {ResultLength} chars", functionName, result.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
                    messages.Add(new ToolChatMessage(toolCall.Id, $"Error: {ex.Message}"));
                }
            }

            // Get next completion with function results
            completion = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        }

        if (functionCallCount >= MaxFunctionCalls)
        {
            _logger.LogWarning("Reached maximum function call limit ({Max})", MaxFunctionCalls);
        }

        return completion;
    }

    /// <summary>
    /// Executes a function call by name with the provided arguments.
    /// Uses strongly-typed parameter classes for type safety and cleaner code.
    /// </summary>
    private async Task<string> ExecuteFunctionAsync(string functionName, string argumentsJson, CancellationToken cancellationToken)
    {
        switch (functionName)
        {
            case "get_full_file_content":
                {
                    var args = JsonSerializer.Deserialize<FunctionParameters.GetFullFileContentParameters>(argumentsJson)
                        ?? throw new InvalidOperationException("Failed to deserialize arguments");
                    return await _contextRetriever.GetFullFileContentAsync(args.FilePath);
                }

            case "get_file_at_commit":
                {
                    var args = JsonSerializer.Deserialize<FunctionParameters.GetFileAtCommitParameters>(argumentsJson)
                        ?? throw new InvalidOperationException("Failed to deserialize arguments");
                    return await _contextRetriever.GetFileAtCommitAsync(args.FilePath, args.CommitOrBranch);
                }

            case "search_codebase":
                {
                    var args = JsonSerializer.Deserialize<FunctionParameters.SearchCodebaseParameters>(argumentsJson)
                        ?? throw new InvalidOperationException("Failed to deserialize arguments");
                    return await _contextRetriever.SearchCodebaseAsync(
                        args.SearchTerm,
                        args.FilePattern,
                        args.MaxResults ?? 10);
                }

            case "get_related_files":
                {
                    var args = JsonSerializer.Deserialize<FunctionParameters.GetRelatedFilesParameters>(argumentsJson)
                        ?? throw new InvalidOperationException("Failed to deserialize arguments");
                    return await _contextRetriever.GetRelatedFilesAsync(args.FilePath);
                }

            case "get_file_history":
                {
                    var args = JsonSerializer.Deserialize<FunctionParameters.GetFileHistoryParameters>(argumentsJson)
                        ?? throw new InvalidOperationException("Failed to deserialize arguments");
                    return await _contextRetriever.GetFileHistoryAsync(
                        args.FilePath,
                        args.MaxCommits ?? 5);
                }

            default:
                return $"Unknown function: {functionName}";
        }
    }
}
