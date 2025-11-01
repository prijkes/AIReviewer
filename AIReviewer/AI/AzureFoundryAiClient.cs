using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AIReviewer.Utils;
using AIReviewer.Diff;
using AIReviewer.Options;
using AIReviewer.Review;
using Azure;
using Azure.AI.OpenAI;

namespace AIReviewer.AI;

/// <summary>
/// Azure OpenAI client implementation for AI-powered code reviews.
/// Sends code diffs and PR metadata to Azure OpenAI for analysis and returns structured issues.
/// </summary>
public sealed class AzureFoundryAiClient : IAiClient
{
    private readonly ILogger<AzureFoundryAiClient> _logger;
    private readonly ReviewerOptions _options;
    private readonly AzureOpenAIClient _client;
    private readonly RetryPolicyFactory _retryFactory;

    /// <summary>
    /// System prompt that instructs the AI on how to perform code reviews and format responses.
    /// </summary>
    private const string SystemPrompt = """
    You are an expert C#/.NET code reviewer bot enforcing security, correctness, performance, readability, and testability. 
    Respond in JSON with schema:
    {
      "issues": [
        {
          "id": "...",
          "title": "...",
          "severity": "info|warn|error",
          "category": "security|correctness|style|performance|docs|tests",
          "file": "path/to/file",
          "line": 123,
          "rationale": "...",
          "recommendation": "...",
          "fix_example": "..."
        }
      ],
      "summary": ""
    }
    Do not include code content unless needed for fix_example. Evaluate only actionable issues.
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
        var endpoint = new Uri(_options.AiFoundryEndpoint);
        var credential = new AzureKeyCredential(_options.AiFoundryApiKey);
        _client = new AzureOpenAIClient(endpoint, credential);
    }

    /// <inheritdoc/>
    public async Task<AiReviewResponse> ReviewAsync(string policy, FileDiff fileDiff, CancellationToken cancellationToken)
    {
        var prompt = CreateUserPrompt(policy, fileDiff);
        var response = await InvokeModelAsync(prompt, cancellationToken);
        return ParseResponse(response);
    }

    /// <inheritdoc/>
    public async Task<AiReviewResponse> ReviewPullRequestMetadataAsync(string policy, PrMetadata metadata, CancellationToken cancellationToken)
    {
        var prompt = $"""
        Review the PR metadata for hygiene and completeness.

        Title: {metadata.Title}
        Description: {metadata.Description}
        Commits:
        {string.Join(Environment.NewLine, metadata.CommitMessages.Take(10))}

        Provide actionable feedback only if something is missing or incorrect. Otherwise return empty issues.
        """;

        var response = await InvokeModelAsync(policy + "\n\nMetadata review rubric: Ensure descriptive title, summary of changes, tests documented.", cancellationToken, prompt);
        return ParseResponse(response);
    }

    /// <summary>
    /// Invokes the Azure OpenAI model with the provided policy and user prompt.
    /// </summary>
    /// <param name="policy">The review policy to apply.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <param name="userPrompt">Optional user prompt to customize the request.</param>
    /// <returns>The raw JSON response from the AI model.</returns>
    private async Task<string> InvokeModelAsync(string policy, CancellationToken cancellationToken, string? userPrompt = null)
    {
        var options = new ChatCompletionsOptions
        {
            DeploymentName = _options.AiFoundryDeployment,
            Temperature = (float)_options.AiTemperature,
            MaxOutputTokens = _options.AiMaxTokens
        };

        options.Messages.Add(new ChatRequestSystemMessage(SystemPrompt));
        options.Messages.Add(new ChatRequestSystemMessage("Policy:\n" + policy));
        options.Messages.Add(new ChatRequestUserMessage(userPrompt ?? policy));
        options.Messages.Add(new ChatRequestAssistantMessage("Respond with the JSON envelope only."));
        options.Messages.Add(new ChatRequestUserMessage(userPrompt ?? throw new InvalidOperationException("User prompt missing")));

        var retry = _retryFactory.CreateHttpRetryPolicy(nameof(OpenAIClient));
        var completions = await retry.ExecuteAsync(() => _client.GetChatCompletionsAsync(options, cancellationToken));
        var content = completions.Value.Choices.First().Message.Content.FirstOrDefault()?.Text ?? "{}";
        _logger.LogInformation("AI response size {Size} bytes", content.Length);
        return content;
    }

    /// <summary>
    /// Parses the raw JSON response from the AI model into a structured review response.
    /// </summary>
    /// <param name="content">The raw JSON content from the AI model.</param>
    /// <returns>A structured <see cref="AiReviewResponse"/> containing the identified issues.</returns>
    /// <exception cref="InvalidDataException">Thrown when required fields are missing from the response.</exception>
    private static AiReviewResponse ParseResponse(string content)
    {
        var envelope = JsonHelpers.DeserializeStrict<AiEnvelope>(content);

        var issues = envelope.Issues.Select(issue => new AiIssue(
            issue.Id ?? throw new InvalidDataException("issue.id missing"),
            issue.Title ?? throw new InvalidDataException("issue.title missing"),
            issue.Severity?.ToLowerInvariant() ?? "info",
            issue.Category?.ToLowerInvariant() ?? "style",
            issue.File ?? string.Empty,
            issue.Line,
            issue.Rationale ?? string.Empty,
            issue.Recommendation ?? string.Empty,
            issue.FixExample
        )).ToList();

        return new AiReviewResponse(issues);
    }

    /// <summary>
    /// Creates a user prompt for reviewing a file diff by combining the policy and diff content.
    /// </summary>
    /// <param name="policy">The review policy.</param>
    /// <param name="fileDiff">The file diff to review.</param>
    /// <returns>A formatted prompt string.</returns>
    private static string CreateUserPrompt(string policy, FileDiff fileDiff)
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
    /// </summary>
    /// <param name="Issues">List of issue items from the response.</param>
    /// <param name="Summary">Optional summary from the response.</param>
    private sealed record AiEnvelope(List<AiItem> Issues, string? Summary);

    /// <summary>
    /// Internal record representing a single issue item in the AI response JSON.
    /// </summary>
    private sealed record AiItem(
        string? Id,
        string? Title,
        string? Severity,
        string? Category,
        string? File,
        int Line,
        string? Rationale,
        string? Recommendation,
        string? FixExample);
}
