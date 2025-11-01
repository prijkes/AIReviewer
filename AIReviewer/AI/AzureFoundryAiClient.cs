using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AIReviewer.Utils;
using AIReviewer.Diff;
using AIReviewer.Options;
using AIReviewer.Review;
using AIReviewer.AzureDevOps.Models;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace AIReviewer.AI;

/// <summary>
/// Azure OpenAI client implementation for AI-powered code reviews.
/// Sends code diffs and PR metadata to Azure OpenAI for analysis and returns structured issues.
/// </summary>
public sealed class AzureFoundryAiClient : IAiClient
{
    private readonly ILogger<AzureFoundryAiClient> _logger;
    private readonly ReviewerOptions _options;
    private readonly OpenAIClient _client;
    private readonly RetryPolicyFactory _retryFactory;

    /// <summary>
    /// System prompt that instructs the AI on how to perform code reviews.
    /// Note: JSON schema is enforced via structured outputs, not via prompt instructions.
    /// </summary>
    private const string SystemPrompt = """
    You are an expert C#/.NET code reviewer bot enforcing security, correctness, performance, readability, and testability.
    Evaluate only actionable issues. Do not include code content unless needed for fix_example.
    """;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureFoundryAiClient"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="options">Configuration options for the reviewer.</param>
    /// <param name="retryFactory">Factory for creating retry policies.</param>
    public AzureFoundryAiClient(ILogger<AzureFoundryAiClient> logger, IOptionsMonitor<ReviewerOptions> options, RetryPolicyFactory retryFactory)
    {
        _logger = logger;
        _options = options.CurrentValue;
        _retryFactory = retryFactory;
        
        _client = new OpenAIClient(
            credential: new ApiKeyCredential(_options.AiFoundryApiKey),
            options: new OpenAIClientOptions()
            {
                Endpoint = new Uri(_options.AiFoundryEndpoint)
            }
        );
    }

    /// <inheritdoc/>
    public async Task<AiReviewResponse> ReviewAsync(string policy, ReviewFileDiff fileDiff, CancellationToken cancellationToken)
    {
        var prompt = CreateUserPrompt(fileDiff);
        var systemPrompt = $"{SystemPrompt}\n\nPolicy:\n{policy}";
        
        // Use ChatClient for structured outputs support
        var chatClient = _client.GetChatClient(_options.AiFoundryDeployment);
        
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(prompt)
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
        
        _logger.LogInformation("AI response size {Size} bytes", content.Length);
        return ParseResponse(content);
    }

    /// <inheritdoc/>
    public async Task<AiReviewResponse> ReviewPullRequestMetadataAsync(string policy, PullRequestMetadata metadata, CancellationToken cancellationToken)
    {
        var prompt = $"""
        Review the PR metadata for hygiene and completeness.

        Title: {metadata.Title}
        Description: {metadata.Description}
        Commits:
        {string.Join(Environment.NewLine, metadata.CommitMessages.Take(10))}

        Provide actionable feedback only if something is missing or incorrect. Otherwise return empty issues.
        """;

        var systemPrompt = $"{SystemPrompt}\n\nPolicy:\n{policy}\n\nMetadata review rubric: Ensure descriptive title, summary of changes, tests documented.";
        
        // Use ChatClient for structured outputs support
        var chatClient = _client.GetChatClient(_options.AiFoundryDeployment);
        
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(prompt)
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

    /// <summary>
    /// Creates a user prompt for reviewing a file diff.
    /// </summary>
    /// <param name="fileDiff">The file diff to review.</param>
    /// <returns>A formatted prompt string.</returns>
    private static string CreateUserPrompt(ReviewFileDiff fileDiff)
    {
        var truncatedDiff = fileDiff.DiffText.Length > 8000 ? fileDiff.DiffText[..8000] : fileDiff.DiffText;
        return $"""
        File: {fileDiff.Path}
        Unified Diff:
        {truncatedDiff}

        Apply the policy rubric. Report up to 5 actionable issues. Leave summary empty.
        """;
    }

    /// <summary>
    /// Internal envelope record for deserializing the AI response JSON.
    /// With structured outputs, all required fields are guaranteed to be non-null.
    /// </summary>
    /// <param name="Issues">List of issue items from the response.</param>
    private sealed record AiEnvelope(List<AiItem> Issues);

    /// <summary>
    /// Internal record representing a single issue item in the AI response JSON.
    /// With structured outputs, enum values and required fields are guaranteed by the schema.
    /// </summary>
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
