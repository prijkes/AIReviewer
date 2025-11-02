using AIReviewer.AI;
using AIReviewer.Diff;
using AIReviewer.Options;
using AIReviewer.Review;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIReviewer.Tests.AI;

public class PromptBuilderTests
{
    private readonly Mock<ILogger<PromptBuilder>> _loggerMock;
    private readonly Mock<IOptionsMonitor<ReviewerOptions>> _optionsMock;
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
        _promptBuilder = new PromptBuilder(_loggerMock.Object, _optionsMock.Object);
    }

    [Fact]
    public void BuildFileReviewSystemPrompt_WithEnglish_ShouldIncludeEnglishInstruction()
    {
        // Arrange
        var policy = "Test policy";
        var language = "en";

        // Act
        var result = _promptBuilder.BuildFileReviewSystemPrompt(policy, language);

        // Assert
        result.Should().Contain("Test policy");
        result.Should().Contain("Provide all review feedback in English language");
        result.Should().NotContain("Japanese");
    }

    [Fact]
    public void BuildFileReviewSystemPrompt_WithJapanese_ShouldIncludeJapaneseInstruction()
    {
        // Arrange
        var policy = "テストポリシー";
        var language = "ja";

        // Act
        var result = _promptBuilder.BuildFileReviewSystemPrompt(policy, language);

        // Assert
        result.Should().Contain("テストポリシー");
        result.Should().Contain("Provide all review feedback in Japanese language");
    }

    [Fact]
    public void BuildFileReviewUserPrompt_WithSmallDiff_ShouldIncludeFullDiff()
    {
        // Arrange
        var diff = new ReviewFileDiff("test.cs", "small diff content", "hash123", false);

        // Act
        var result = _promptBuilder.BuildFileReviewUserPrompt(diff);

        // Assert
        result.Should().Contain("test.cs");
        result.Should().Contain("small diff content");
        result.Should().Contain("Apply the policy rubric");
    }

    [Fact]
    public void BuildFileReviewUserPrompt_WithLargeDiff_ShouldTruncateDiff()
    {
        // Arrange
        var largeDiff = new string('x', 2000);
        var diff = new ReviewFileDiff("test.cs", largeDiff, "hash123", false);

        // Act
        var result = _promptBuilder.BuildFileReviewUserPrompt(diff);

        // Assert
        result.Should().Contain("test.cs");
        result.Should().NotContain(largeDiff);
        result.Length.Should().BeLessThan(largeDiff.Length + 500); // Prompt overhead
    }

    [Fact]
    public void BuildMetadataReviewSystemPrompt_ShouldIncludeMetadataRubric()
    {
        // Arrange
        var policy = "Test policy";
        var language = "en";

        // Act
        var result = _promptBuilder.BuildMetadataReviewSystemPrompt(policy, language);

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
}
