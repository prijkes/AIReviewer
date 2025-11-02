using AIReviewer.Diff;
using AIReviewer.Options;
using AIReviewer.Review;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIReviewer.AI;

/// <summary>
/// Builds prompts for AI code review requests.
/// Centralizes prompt construction logic for consistency and maintainability.
/// </summary>
public sealed class PromptBuilder
{
    private readonly ILogger<PromptBuilder> _logger;
    private readonly ReviewerOptions _options;

    /// <summary>
    /// Base system prompt that instructs the AI on how to perform code reviews.
    /// </summary>
    private const string BaseSystemPrompt = """
        You are an expert C#/.NET code reviewer bot enforcing security, correctness, performance, readability, and testability.
        Evaluate only actionable issues. Do not include code content unless needed for fix_example.
        """;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptBuilder"/> class.
    /// </summary>
    public PromptBuilder(ILogger<PromptBuilder> logger, IOptionsMonitor<ReviewerOptions> options)
    {
        _logger = logger;
        _options = options.CurrentValue;
    }

    /// <summary>
    /// Builds a system prompt for file review with policy and language instructions.
    /// </summary>
    /// <param name="policy">The review policy to apply.</param>
    /// <param name="language">Language code for the review response.</param>
    /// <returns>The complete system prompt.</returns>
    public string BuildFileReviewSystemPrompt(string policy, string language)
    {
        var languageInstruction = language == "ja"
            ? "\n\nIMPORTANT: Provide all review feedback in Japanese language."
            : "\n\nIMPORTANT: Provide all review feedback in English language.";

        return $"{BaseSystemPrompt}\n\nPolicy:\n{policy}{languageInstruction}";
    }

    /// <summary>
    /// Builds a user prompt for reviewing a file diff.
    /// </summary>
    /// <param name="fileDiff">The file diff to review.</param>
    /// <returns>The formatted user prompt.</returns>
    public string BuildFileReviewUserPrompt(ReviewFileDiff fileDiff)
    {
        var truncatedDiff = fileDiff.DiffText;
        if (fileDiff.DiffText.Length > _options.MaxPromptDiffBytes)
        {
            truncatedDiff = fileDiff.DiffText[.._options.MaxPromptDiffBytes];
            _logger.LogDebug("Truncating diff in prompt for {Path} ({Original} bytes -> {Truncated} bytes)",
                fileDiff.Path, fileDiff.DiffText.Length, _options.MaxPromptDiffBytes);
        }

        return $"""
            File: {fileDiff.Path}
            Unified Diff:
            {truncatedDiff}

            Apply the policy rubric. Report up to 5 actionable issues. Leave summary empty.
            """;
    }

    /// <summary>
    /// Builds a system prompt for PR metadata review with policy and language instructions.
    /// </summary>
    /// <param name="policy">The review policy to apply.</param>
    /// <param name="language">Language code for the review response.</param>
    /// <returns>The complete system prompt.</returns>
    public string BuildMetadataReviewSystemPrompt(string policy, string language)
    {
        var languageInstruction = language == "ja"
            ? "\n\nIMPORTANT: Provide all review feedback in Japanese language."
            : "\n\nIMPORTANT: Provide all review feedback in English language.";

        return $"{BaseSystemPrompt}\n\nPolicy:\n{policy}\n\nMetadata review rubric: Ensure descriptive title, summary of changes, tests documented.{languageInstruction}";
    }

    /// <summary>
    /// Builds a user prompt for reviewing pull request metadata.
    /// </summary>
    /// <param name="metadata">The PR metadata to review.</param>
    /// <returns>The formatted user prompt.</returns>
    public string BuildMetadataReviewUserPrompt(PullRequestMetadata metadata)
    {
        var commitMessages = metadata.CommitMessages.Take(_options.MaxCommitMessagesToReview).ToList();

        if (metadata.CommitMessages.Count > _options.MaxCommitMessagesToReview)
        {
            _logger.LogDebug("Truncating commit messages for metadata review ({Total} commits -> {Max} commits)",
                metadata.CommitMessages.Count, _options.MaxCommitMessagesToReview);
        }

        return $"""
            Review the PR metadata for hygiene and completeness.

            Title: {metadata.Title}
            Description: {metadata.Description}
            Commits:
            {string.Join(Environment.NewLine, commitMessages)}

            Provide actionable feedback only if something is missing or incorrect. Otherwise return empty issues.
            """;
    }
}
