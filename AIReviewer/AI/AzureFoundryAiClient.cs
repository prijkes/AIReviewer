using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AIReviewer.Utils;
using AIReviewer.Diff;
using AIReviewer.Options;
using AIReviewer.Review;
using Azure;
using Azure.AI.OpenAI;

namespace AIReviewer.AI;

public sealed class AzureFoundryAiClient : IAiClient
{
    private readonly ILogger<AzureFoundryAiClient> _logger;
    private readonly ReviewerOptions _options;
    private readonly AzureOpenAIClient _client;
    private readonly RetryPolicyFactory _retryFactory;

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

    public AzureFoundryAiClient(ILogger<AzureFoundryAiClient> logger, IOptionsMonitor<ReviewerOptions> options, RetryPolicyFactory retryFactory)
    {
        _logger = logger;
        _options = options.CurrentValue;
        _retryFactory = retryFactory;
        var endpoint = new Uri(_options.AiFoundryEndpoint);
        var credential = new AzureKeyCredential(_options.AiFoundryApiKey);
        _client = new AzureOpenAIClient(endpoint, credential);
    }

    public async Task<AiReviewResponse> ReviewAsync(string policy, FileDiff fileDiff, CancellationToken cancellationToken)
    {
        var prompt = CreateUserPrompt(policy, fileDiff);
        var response = await InvokeModelAsync(prompt, cancellationToken);
        return ParseResponse(response);
    }

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

    private sealed record AiEnvelope(List<AiItem> Issues, string? Summary);

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
