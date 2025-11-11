using AIReviewer.AI;
using AIReviewer.AzureDevOps.Models;
using AIReviewer.Diff;
using AIReviewer.Options;
using AIReviewer.Review;
using AIReviewer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIReviewer.Tests.AI;

public class PromptBuilderTests
{
    private readonly Mock<ILogger<PromptBuilder>> _loggerMock;
    private readonly Mock<IOptionsMonitor<ReviewerOptions>> _optionsMock;
    private readonly Mock<PromptLoader> _promptLoaderMock;
    private readonly ReviewerOptions _options;
    private readonly PromptBuilder _promptBuilder;

    public PromptBuilderTests()
    {
        _loggerMock = new Mock<ILogger<PromptBuilder>>();
        _optionsMock = new Mock<IOptionsMonitor<ReviewerOptions>>();
        _options = new ReviewerOptions
        {
            MaxPromptDiffBytes = 1000,
            MaxCommitMessagesToReview = 5
        };
        _optionsMock.Setup(x => x.CurrentValue).Returns(_options);

        // Mock PromptLoader
        var promptLoaderLoggerMock = new Mock<ILogger<PromptLoader>>();
        _promptLoaderMock = new Mock<PromptLoader>(promptLoaderLoggerMock.Object, _optionsMock.Object);

        // Setup mock responses for different prompts
        _promptLoaderMock.Setup(x => x.LoadSystemPromptAsync(It.IsAny<ProgrammingLanguageDetector.ProgrammingLanguage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProgrammingLanguageDetector.ProgrammingLanguage lang, CancellationToken ct) =>
                lang == ProgrammingLanguageDetector.ProgrammingLanguage.CSharp
                    ? "You are an expert C#/.NET code reviewer"
                    : lang == ProgrammingLanguageDetector.ProgrammingLanguage.Cpp
                        ? "You are an expert C++ code reviewer"
                        : "You are an expert code reviewer");

        _promptLoaderMock.Setup(x => x.LoadLanguageInstructionAsync("en", It.IsAny<CancellationToken>()))
            .ReturnsAsync("IMPORTANT: Provide all review feedback in English language.");

        _promptLoaderMock.Setup(x => x.LoadLanguageInstructionAsync("ja", It.IsAny<CancellationToken>()))
            .ReturnsAsync("IMPORTANT: Provide all review feedback in Japanese language.");

        _promptLoaderMock.Setup(x => x.LoadFileReviewInstructionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("Apply the policy rubric. Report up to 5 actionable issues. Leave summary empty.");

        _promptLoaderMock.Setup(x => x.LoadMetadataReviewInstructionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("Review the PR metadata for hygiene and completeness.\n\nMetadata review rubric: Ensure descriptive title, summary of changes, tests documented.");

        _promptBuilder = new PromptBuilder(_loggerMock.Object, _optionsMock.Object, _promptLoaderMock.Object);
    }

    [Fact]
    public async Task BuildFileReviewSystemPrompt_WithEnglish_ShouldIncludeEnglishInstruction()
    {
        // Arrange
        var policy = "Test policy";
        var language = "en";
        var programmingLanguage = ProgrammingLanguageDetector.ProgrammingLanguage.CSharp;

        // Act
        var result = await _promptBuilder.BuildFileReviewSystemPromptAsync(policy, language, programmingLanguage, CancellationToken.None);

        // Assert
        result.Should().Contain("Test policy");
        result.Should().Contain("Provide all review feedback in English language");
        result.Should().Contain("C#/.NET");
    }

    [Fact]
    public async Task BuildFileReviewSystemPrompt_WithJapanese_ShouldIncludeJapaneseInstruction()
    {
        // Arrange
        var policy = "テストポリシー";
        var language = "ja";
        var programmingLanguage = ProgrammingLanguageDetector.ProgrammingLanguage.Cpp;

        // Act
        var result = await _promptBuilder.BuildFileReviewSystemPromptAsync(policy, language, programmingLanguage, CancellationToken.None);

        // Assert
        result.Should().Contain("テストポリシー");
        result.Should().Contain("Provide all review feedback in Japanese language");
        result.Should().Contain("C++");
    }

    [Fact]
    public async Task BuildFileReviewUserPrompt_WithSmallDiff_ShouldIncludeFullDiff()
    {
        // Arrange
        var diff = new ReviewFileDiff("test.cs", "small diff content", "hash123", false, false);
        var existingComments = new List<ExistingComment>();

        // Act
        var result = await _promptBuilder.BuildFileReviewUserPromptAsync(diff, existingComments, CancellationToken.None);

        // Assert
        result.Should().Contain("test.cs");
        result.Should().Contain("small diff content");
        result.Should().Contain("Apply the policy rubric");
    }

    [Fact]
    public async Task BuildFileReviewUserPrompt_WithLargeDiff_ShouldTruncateDiff()
    {
        // Arrange
        var largeDiff = new string('x', 2000);
        var diff = new ReviewFileDiff("test.cs", largeDiff, "hash123", false, false);
        var existingComments = new List<ExistingComment>();

        // Act
        var result = await _promptBuilder.BuildFileReviewUserPromptAsync(diff, existingComments, CancellationToken.None);

        // Assert
        result.Should().Contain("test.cs");
        result.Should().NotContain(largeDiff);
        result.Length.Should().BeLessThan(largeDiff.Length + 500); // Prompt overhead
    }

    [Fact]
    public async Task BuildMetadataReviewSystemPrompt_ShouldIncludeMetadataRubric()
    {
        // Arrange
        var policy = "Test policy";
        var language = "en";

        // Act
        var result = await _promptBuilder.BuildMetadataReviewSystemPromptAsync(policy, language, CancellationToken.None);

        // Assert
        result.Should().Contain("Test policy");
        result.Should().Contain("Metadata review rubric");
        result.Should().Contain("descriptive title");
    }

    [Fact]
    public void BuildMetadataReviewUserPrompt_WithFewCommits_ShouldIncludeAllCommits()
    {
        // Arrange
        var metadata = new PullRequestMetadata(
            "Fix bug",
            "Fixed authentication bug",
            ["Commit 1", "Commit 2", "Commit 3"]
        );

        // Act
        var result = _promptBuilder.BuildMetadataReviewUserPrompt(metadata);

        // Assert
        result.Should().Contain("Fix bug");
        result.Should().Contain("Fixed authentication bug");
        result.Should().Contain("Commit 1");
        result.Should().Contain("Commit 2");
        result.Should().Contain("Commit 3");
    }

    [Fact]
    public void BuildMetadataReviewUserPrompt_WithManyCommits_ShouldTruncateCommits()
    {
        // Arrange
        var commits = Enumerable.Range(1, 10).Select(i => $"Commit {i}").ToList();
        var metadata = new PullRequestMetadata("Fix bug", "Description", commits);

        // Act
        var result = _promptBuilder.BuildMetadataReviewUserPrompt(metadata);

        // Assert
        result.Should().Contain("Commit 1");
        result.Should().Contain("Commit 5");
        result.Should().NotContain("Commit 6");
        result.Should().NotContain("Commit 10");
    }

    [Fact]
    public async Task BuildFileReviewUserPrompt_WithDeletedFile_ShouldIncludeDeletedFileNotice()
    {
        // Arrange
        var diff = new ReviewFileDiff("deleted.cs", "diff content", "hash123", false, true);
        var existingComments = new List<ExistingComment>();

        // Act
        var result = await _promptBuilder.BuildFileReviewUserPromptAsync(diff, existingComments, CancellationToken.None);

        // Assert
        result.Should().Contain("deleted.cs");
        result.Should().Contain("FILE STATUS: This file has been DELETED");
        result.Should().Contain("use the line numbers from the ORIGINAL file");
        result.Should().Contain("shown with '-' prefix in the diff");
        result.Should().Contain("HOW TO COUNT LINE NUMBERS IN THIS DIFF:");
        result.Should().Contain("FIRST line with '-' after the '@@' header is the starting line number");
        result.Should().Contain("@@ -1,115 +0,0 @@");
        result.Should().Contain("-# azure-pipelines.yml     <- This is line 1 of the original file");
    }

    [Fact]
    public async Task BuildFileReviewUserPrompt_WithNonDeletedFile_ShouldNotIncludeDeletedFileNotice()
    {
        // Arrange
        var diff = new ReviewFileDiff("modified.cs", "diff content", "hash123", false, false);
        var existingComments = new List<ExistingComment>();

        // Act
        var result = await _promptBuilder.BuildFileReviewUserPromptAsync(diff, existingComments, CancellationToken.None);

        // Assert
        result.Should().Contain("modified.cs");
        result.Should().NotContain("FILE STATUS: This file has been DELETED");
        result.Should().NotContain("ORIGINAL file");
    }

    [Fact]
    public async Task BuildFileReviewUserPrompt_WithExistingComments_ShouldIncludeExistingCommentsSection()
    {
        // Arrange
        var diff = new ReviewFileDiff("test.cs", "diff content", "hash123", false, false);
        var existingComments = new List<ExistingComment>
        {
            new("Alice", "Missing null check", "test.cs", 42, "Active"),
            new("Bob", "Consider using async", "test.cs", null, "Active")
        };

        // Act
        var result = await _promptBuilder.BuildFileReviewUserPromptAsync(diff, existingComments, CancellationToken.None);

        // Assert
        result.Should().Contain("Existing Comments on this file:");
        result.Should().Contain("[Alice] (Line 42): Missing null check");
        result.Should().Contain("[Bob]: Consider using async");
        result.Should().Contain("Do NOT create issues that duplicate or overlap");
    }
}
