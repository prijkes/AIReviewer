using AIReviewer.AzureDevOps.Models;
using AIReviewer.Diff;
using AIReviewer.Options;
using AIReviewer.Review;
using AIReviewer.Utils;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;

namespace AIReviewer.AI.Providers;

/// <summary>
/// Azure OpenAI implementation of the AI provider interface.
/// </summary>
public sealed class AzureOpenAiProvider : IAiProvider
{
    private readonly ILogger<AzureOpenAiProvider> _logger;
    private readonly ReviewerOptions _options;
    private readonly AzureOpenAIClient _client;
    private readonly PromptBuilder _promptBuilder;
    private readonly ReviewContextRetriever? _contextRetriever;
    private long _lastInputTokens;
    private long _lastOutputTokens;

    public string ProviderName => "Azure OpenAI";

    public AzureOpenAiProvider(
        ILogger<AzureOpenAiProvider> logger,
        IOptionsMonitor<ReviewerOptions> options,
        PromptBuilder promptBuilder,
        ReviewContextRetriever? contextRetriever = null)
    {
        _logger = logger;
        _options = options.CurrentValue;
        _promptBuilder = promptBuilder;
        _contextRetriever = contextRetriever;

        _client = new AzureOpenAIClient(
            new Uri(_options.AiFoundryEndpoint),
            new ApiKeyCredential(_options.AiFoundryApiKey)
        );
    }

    public async Task<AiReviewResponse> ReviewAsync(string policy, ReviewFileDiff fileDiff, string language, ProgrammingLanguageDetector.ProgrammingLanguage programmingLanguage, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Requesting {Provider} review for {Path} ({DiffSize} bytes) in language: {Language}, programming language: {ProgrammingLanguage}",
            ProviderName, fileDiff.Path, fileDiff.DiffText.Length, language, ProgrammingLanguageDetector.GetDisplayName(programmingLanguage));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var chatClient = _client.GetChatClient(_options.AiFoundryDeployment);

        var systemPrompt = await _promptBuilder.BuildFileReviewSystemPromptAsync(policy, language, programmingLanguage, cancellationToken);
        var userPrompt = await _promptBuilder.BuildFileReviewUserPromptAsync(fileDiff, cancellationToken);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = _options.AiMaxTokens,
            Temperature = (float)_options.AiTemperature,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "ai_review_response",
                jsonSchema: AiResponseSchemaGenerator.GetResponseSchema(),
                jsonSchemaIsStrict: true)
        };

        // Add function calling tools if enabled
        if (_options.EnableFunctionCalling && _contextRetriever != null)
        {
            foreach (var tool in FunctionDefinitions.GetAllDefinitions())
            {
                options.Tools.Add(tool);
            }
        }

        var completion = await chatClient.CompleteChatAsync(messages, options, cancellationToken);

        // Handle function calls if present
        if (_options.EnableFunctionCalling && _contextRetriever != null)
        {
            completion = await HandleFunctionCallsAsync(chatClient, messages, options, completion, cancellationToken);
        }

        var content = completion.Value.Content[0].Text;

        stopwatch.Stop();
        var response = ParseResponse(content);

        // Store token usage
        if (completion.Value.Usage != null)
        {
            _lastInputTokens = completion.Value.Usage.InputTokenCount;
            _lastOutputTokens = completion.Value.Usage.OutputTokenCount;
        }

        _logger.LogInformation("{Provider} reviewed {Path}: {IssueCount} issues found in {Ms}ms",
            ProviderName, fileDiff.Path, response.Issues.Count, stopwatch.ElapsedMilliseconds);

        return response;
    }

    public async Task<AiReviewResponse> ReviewPullRequestMetadataAsync(string policy, PullRequestMetadata metadata, string language, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Requesting {Provider} metadata review in language: {Language}", ProviderName, language);

        var chatClient = _client.GetChatClient(_options.AiFoundryDeployment);

        var systemPrompt = await _promptBuilder.BuildMetadataReviewSystemPromptAsync(policy, language, cancellationToken);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(_promptBuilder.BuildMetadataReviewUserPrompt(metadata))
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = _options.AiMaxTokens,
            Temperature = (float)_options.AiTemperature,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "ai_review_response",
                jsonSchema: AiResponseSchemaGenerator.GetResponseSchema(),
                jsonSchemaIsStrict: true)
        };

        var completion = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        var content = completion.Value.Content[0].Text;

        // Store token usage
        if (completion.Value.Usage != null)
        {
            _lastInputTokens = completion.Value.Usage.InputTokenCount;
            _lastOutputTokens = completion.Value.Usage.OutputTokenCount;
        }

        return ParseResponse(content);
    }

    public (long InputTokens, long OutputTokens) GetLastTokenUsage()
    {
        return (_lastInputTokens, _lastOutputTokens);
    }

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

            messages.Add(new AssistantChatMessage(completion.Value));

            foreach (var toolCall in completion.Value.ToolCalls)
            {
                if (toolCall.Kind != ChatToolCallKind.Function || _contextRetriever == null)
                {
                    continue;
                }

                var functionName = toolCall.FunctionName;
                var functionArgs = toolCall.FunctionArguments;

                _logger.LogInformation("Executing function: {FunctionName}", functionName);

                try
                {
                    var result = await ExecuteFunctionAsync(functionName, functionArgs.ToString(), cancellationToken);
                    messages.Add(new ToolChatMessage(toolCall.Id, result));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
                    messages.Add(new ToolChatMessage(toolCall.Id, $"Error: {ex.Message}"));
                }
            }

            completion = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        }

        return completion;
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string argumentsJson, CancellationToken cancellationToken)
    {
        if (_contextRetriever == null)
        {
            return "Function calling not available";
        }

        return functionName switch
        {
            "get_full_file_content" => await _contextRetriever.GetFullFileContentAsync(
                JsonSerializer.Deserialize<FunctionParameters.GetFullFileContentParameters>(argumentsJson)?.FilePath ?? ""),
            "get_file_at_commit" => await _contextRetriever.GetFileAtCommitAsync(
                JsonSerializer.Deserialize<FunctionParameters.GetFileAtCommitParameters>(argumentsJson)?.FilePath ?? "",
                JsonSerializer.Deserialize<FunctionParameters.GetFileAtCommitParameters>(argumentsJson)?.CommitOrBranch ?? ""),
            "search_codebase" => await _contextRetriever.SearchCodebaseAsync(
                JsonSerializer.Deserialize<FunctionParameters.SearchCodebaseParameters>(argumentsJson)?.SearchTerm ?? "",
                JsonSerializer.Deserialize<FunctionParameters.SearchCodebaseParameters>(argumentsJson)?.FilePattern,
                JsonSerializer.Deserialize<FunctionParameters.SearchCodebaseParameters>(argumentsJson)?.MaxResults ?? 10),
            "get_related_files" => await _contextRetriever.GetRelatedFilesAsync(
                JsonSerializer.Deserialize<FunctionParameters.GetRelatedFilesParameters>(argumentsJson)?.FilePath ?? ""),
            "get_file_history" => await _contextRetriever.GetFileHistoryAsync(
                JsonSerializer.Deserialize<FunctionParameters.GetFileHistoryParameters>(argumentsJson)?.FilePath ?? "",
                JsonSerializer.Deserialize<FunctionParameters.GetFileHistoryParameters>(argumentsJson)?.MaxCommits ?? 5),
            _ => $"Unknown function: {functionName}"
        };
    }

    private static AiReviewResponse ParseResponse(string content)
    {
        var envelope = JsonHelpers.DeserializeStrict<AiEnvelope>(content);

        var issues = envelope.Issues.Select(issue => new AiIssue(
            issue.Id,
            issue.Title,
            issue.Severity,
            issue.Category,
            issue.File,
            issue.Line,
            issue.Rationale,
            issue.Recommendation,
            issue.FixExample
        )).ToList();

        return new AiReviewResponse(issues);
    }

    private sealed record AiEnvelope(List<AiItem> Issues);

    private sealed record AiItem(
        string Id,
        string Title,
        IssueSeverity Severity,
        IssueCategory Category,
        string File,
        int Line,
        string Rationale,
        string Recommendation,
        string? FixExample);
}
