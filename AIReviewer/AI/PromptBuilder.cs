using AIReviewer.Diff;
using AIReviewer.Options;
using AIReviewer.Review;
using AIReviewer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIReviewer.AI;

/// <summary>
/// Builds prompts for AI code review requests.
/// Centralizes prompt construction logic for consistency and maintainability.
/// Prompts are now loaded from external markdown files for easy editing without recompilation.
/// </summary>
public sealed class PromptBuilder
{
    private readonly ILogger<PromptBuilder> _logger;
    private readonly ReviewerOptions _options;
    private readonly PromptLoader _promptLoader;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptBuilder"/> class.
    /// </summary>
    public PromptBuilder(ILogger<PromptBuilder> logger, IOptionsMonitor<ReviewerOptions> options, PromptLoader promptLoader)
    {
        _logger = logger;
        _options = options.CurrentValue;
        _promptLoader = promptLoader;
    }

    /// <summary>
    /// Builds a system prompt for file review with policy and language instructions.
    /// </summary>
    /// <param name="policy">The review policy to apply.</param>
    /// <param name="language">Language code for the review response.</param>
    /// <param name="programmingLanguage">The programming language being reviewed.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The complete system prompt.</returns>
    public async Task<string> BuildFileReviewSystemPromptAsync(
        string policy, 
        string language, 
        ProgrammingLanguageDetector.ProgrammingLanguage programmingLanguage,
        CancellationToken cancellationToken)
    {
        var basePrompt = await _promptLoader.LoadSystemPromptAsync(programmingLanguage, cancellationToken);
        var languageInstruction = await _promptLoader.LoadLanguageInstructionAsync(language, cancellationToken);

        return $"{basePrompt}\n\nPolicy:\n{policy}\n\n{languageInstruction}";
    }

    /// <summary>
    /// Builds a user prompt for reviewing a file diff.
    /// </summary>
    /// <param name="fileDiff">The file diff to review.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The formatted user prompt.</returns>
    public async Task<string> BuildFileReviewUserPromptAsync(ReviewFileDiff fileDiff, CancellationToken cancellationToken)
    {
        var truncatedDiff = fileDiff.DiffText;
        if (fileDiff.DiffText.Length > _options.MaxPromptDiffBytes)
        {
            truncatedDiff = fileDiff.DiffText[.._options.MaxPromptDiffBytes];
            _logger.LogDebug("Truncating diff in prompt for {Path} ({Original} bytes -> {Truncated} bytes)",
                fileDiff.Path, fileDiff.DiffText.Length, _options.MaxPromptDiffBytes);
        }

        var instructions = await _promptLoader.LoadFileReviewInstructionAsync(cancellationToken);

        return $"""
            File: {fileDiff.Path}
            Unified Diff:
            {truncatedDiff}

            {instructions}
            """;
    }

    /// <summary>
    /// Builds a system prompt for PR metadata review with policy and language instructions.
    /// </summary>
    /// <param name="policy">The review policy to apply.</param>
    /// <param name="language">Language code for the review response.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The complete system prompt.</returns>
    public async Task<string> BuildMetadataReviewSystemPromptAsync(string policy, string language, CancellationToken cancellationToken)
    {
        // Metadata review uses a generic prompt since it's not language-specific
        var basePrompt = await _promptLoader.LoadSystemPromptAsync(ProgrammingLanguageDetector.ProgrammingLanguage.Unknown, cancellationToken);
        var languageInstruction = await _promptLoader.LoadLanguageInstructionAsync(language, cancellationToken);
        var metadataInstructions = await _promptLoader.LoadMetadataReviewInstructionAsync(cancellationToken);

        return $"{basePrompt}\n\nPolicy:\n{policy}\n\n{metadataInstructions}\n\n{languageInstruction}";
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
