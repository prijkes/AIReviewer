using AIReviewer.AzureDevOps;
using AIReviewer.AzureDevOps.Models;
using AIReviewer.Review;

namespace AIReviewer.Tests.AzureDevOps;

public class CommentFormatterTests
{
    [Fact]
    public void FormatReviewIssue_WithBasicIssue_ShouldFormatCorrectly()
    {
        // Arrange
        var issue = new ReviewIssue
        {
            Title = "Issue description",
            Severity = IssueSeverity.Error,
            Category = IssueCategory.Security,
            FilePath = "test.cs",
            Line = 10,
            Rationale = "This is a security issue",
            Recommendation = "Use SecureString instead",
            FixExample = null,
            Fingerprint = "fingerprint123"
        };

        // Act
        var result = CommentFormatter.FormatReviewIssue(issue);

        // Assert
        result.Should().Contain("🤖 AI Review");
        result.Should().Contain("Security/Error");
        result.Should().Contain("This is a security issue");
        result.Should().Contain("**Recommendation**: Use SecureString instead");
    }

    [Fact]
    public void FormatReviewIssue_WithFixExample_ShouldIncludeCodeBlock()
    {
        // Arrange
        var fixExample = "var secure = new SecureString();";
        var issue = new ReviewIssue
        {
            Title = "Issue description",
            Severity = IssueSeverity.Warn,
            Category = IssueCategory.Performance,
            FilePath = "test.cs",
            Line = 15,
            Rationale = "Should use secure string",
            Recommendation = "Update the code",
            FixExample = fixExample,
            Fingerprint = "fingerprint456"
        };

        // Act
        var result = CommentFormatter.FormatReviewIssue(issue);

        // Assert
        result.Should().Contain("```csharp");
        result.Should().Contain(fixExample);
        result.Should().Contain("```");
    }

    [Fact]
    public void FormatReviewIssue_WithoutFixExample_ShouldNotIncludeCodeBlock()
    {
        // Arrange
        var issue = new ReviewIssue
        {
            Title = "Issue description",
            Severity = IssueSeverity.Info,
            Category = IssueCategory.Style,
            FilePath = "test.cs",
            Line = 20,
            Rationale = "Consider improving naming",
            Recommendation = "Use more descriptive names",
            FixExample = null,
            Fingerprint = "fingerprint789"
        };

        // Act
        var result = CommentFormatter.FormatReviewIssue(issue);

        // Assert
        result.Should().NotContain("```");
    }

    [Fact]
    public void FormatReviewIssue_ShouldIncludeBotDisclaimer()
    {
        // Arrange
        var issue = new ReviewIssue
        {
            Title = "Test",
            Severity = IssueSeverity.Error,
            Category = IssueCategory.Correctness,
            FilePath = "file.cs",
            Line = 1,
            Rationale = "Rationale",
            Recommendation = "Recommendation",
            FixExample = null,
            Fingerprint = "fp1"
        };

        // Act
        var result = CommentFormatter.FormatReviewIssue(issue);

        // Assert
        result.Should().Contain("I'm a bot");
        result.Should().Contain("DRY_RUN=true");
    }

    [Fact]
    public void FormatReviewIssue_WithWarning_ShouldShowWarnSeverity()
    {
        // Arrange
        var issue = new ReviewIssue
        {
            Title = "Warning issue",
            Severity = IssueSeverity.Warn,
            Category = IssueCategory.Performance,
            FilePath = "perf.cs",
            Line = 5,
            Rationale = "This could be optimized",
            Recommendation = "Use async/await",
            FixExample = null,
            Fingerprint = "fp_warn"
        };

        // Act
        var result = CommentFormatter.FormatReviewIssue(issue);

        // Assert
        result.Should().Contain("Performance/Warn");
    }

    [Fact]
    public void FormatReviewIssue_WithInfo_ShouldShowInfoSeverity()
    {
        // Arrange
        var issue = new ReviewIssue
        {
            Title = "Info issue",
            Severity = IssueSeverity.Info,
            Category = IssueCategory.Docs,
            FilePath = "doc.cs",
            Line = 30,
            Rationale = "Add documentation",
            Recommendation = "Add XML comments",
            FixExample = null,
            Fingerprint = "fp_info"
        };

        // Act
        var result = CommentFormatter.FormatReviewIssue(issue);

        // Assert
        result.Should().Contain("Docs/Info");
    }

    [Fact]
    public void FormatReTriggeredIssue_ShouldPrefixWithReTriggered()
    {
        // Arrange
        var issue = new ReviewIssue
        {
            Title = "Retriggered issue",
            Severity = IssueSeverity.Error,
            Category = IssueCategory.Security,
            FilePath = "test.cs",
            Line = 10,
            Rationale = "Security problem",
            Recommendation = "Fix it",
            FixExample = null,
            Fingerprint = "fp_retrig"
        };

        // Act
        var result = CommentFormatter.FormatReTriggeredIssue(issue);

        // Assert
        result.Should().StartWith("Re-triggered:");
        result.Should().Contain("🤖 AI Review");
        result.Should().Contain("Security/Error");
    }

    [Fact]
    public void FormatStateThread_ShouldIncludeHiddenComment()
    {
        // Arrange
        var issues = new List<ReviewIssue>
        {
            new() { Title = "Issue 1", Severity = IssueSeverity.Error, Category = IssueCategory.Correctness, FilePath = "file1.cs", Line = 10, Rationale = "R1", Recommendation = "Rec1", Fingerprint = "fp1" },
            new() { Title = "Issue 2", Severity = IssueSeverity.Warn, Category = IssueCategory.Style, FilePath = "file2.cs", Line = 20, Rationale = "R2", Recommendation = "Rec2", Fingerprint = "fp2" }
        };
        var result = new ReviewPlanResult(issues, 1, 1, 3);

        // Act
        var formatted = CommentFormatter.FormatStateThread(result);

        // Assert
        formatted.Should().Contain("<!-- ai-state -->");
        formatted.Should().Contain("```");
    }

    [Fact]
    public void FormatStateThread_ShouldContainFingerprints()
    {
        // Arrange
        var issues = new List<ReviewIssue>
        {
            new() { Title = "Issue 1", Severity = IssueSeverity.Error, Category = IssueCategory.Correctness, FilePath = "file1.cs", Line = 10, Rationale = "R1", Recommendation = "Rec1", Fingerprint = "fingerprint1" },
            new() { Title = "Issue 2", Severity = IssueSeverity.Warn, Category = IssueCategory.Style, FilePath = "file2.cs", Line = 20, Rationale = "R2", Recommendation = "Rec2", Fingerprint = "fingerprint2" }
        };
        var result = new ReviewPlanResult(issues, 1, 1, 3);

        // Act
        var formatted = CommentFormatter.FormatStateThread(result);

        // Assert
        formatted.Should().Contain("fingerprint1");
        formatted.Should().Contain("fingerprint2");
    }

    [Fact]
    public void FormatStateThread_ShouldContainFilePathsAndLines()
    {
        // Arrange
        var issues = new List<ReviewIssue>
        {
            new() { Title = "Issue", Severity = IssueSeverity.Error, Category = IssueCategory.Correctness, FilePath = "src/test.cs", Line = 42, Rationale = "R", Recommendation = "Rec", Fingerprint = "fp1" }
        };
        var result = new ReviewPlanResult(issues, 1, 0, 3);

        // Act
        var formatted = CommentFormatter.FormatStateThread(result);

        // Assert
        formatted.Should().Contain("src/test.cs");
        formatted.Should().Contain("42");
    }

    [Fact]
    public void FormatStateThread_ShouldContainSeverity()
    {
        // Arrange
        var issues = new List<ReviewIssue>
        {
            new() { Title = "Issue", Severity = IssueSeverity.Error, Category = IssueCategory.Correctness, FilePath = "file.cs", Line = 1, Rationale = "R", Recommendation = "Rec", Fingerprint = "fp1" }
        };
        var result = new ReviewPlanResult(issues, 1, 0, 3);

        // Act
        var formatted = CommentFormatter.FormatStateThread(result);

        // Assert
        formatted.Should().Contain("\"severity\":2");
    }

    [Fact]
    public void FormatStateThread_WithNoIssues_ShouldReturnValidJson()
    {
        // Arrange
        var result = new ReviewPlanResult([], 0, 0, 3);

        // Act
        var formatted = CommentFormatter.FormatStateThread(result);

        // Assert
        formatted.Should().Contain("<!-- ai-state -->");
        formatted.Should().Contain("[]"); // Empty array
    }

    [Fact]
    public void FormatStateThread_ShouldIncludeTimestamp()
    {
        // Arrange
        var issues = new List<ReviewIssue>
        {
            new() { Title = "Issue", Severity = IssueSeverity.Error, Category = IssueCategory.Correctness, FilePath = "file.cs", Line = 1, Rationale = "R", Recommendation = "Rec", Fingerprint = "fp1" }
        };
        var result = new ReviewPlanResult(issues, 1, 0, 3);

        // Act
        var formatted = CommentFormatter.FormatStateThread(result);

        // Assert
        formatted.Should().Contain("updatedAt");
    }

    [Fact]
    public void FormatReviewIssue_WithMultilineFixExample_ShouldFormatCorrectly()
    {
        // Arrange
        var fixExample = @"if (condition)
{
    DoSomething();
}";
        var issue = new ReviewIssue
        {
            Title = "Use braces",
            Severity = IssueSeverity.Warn,
            Category = IssueCategory.Style,
            FilePath = "test.cs",
            Line = 10,
            Rationale = "Always use braces",
            Recommendation = "Add braces",
            FixExample = fixExample,
            Fingerprint = "fp_multiline"
        };

        // Act
        var result = CommentFormatter.FormatReviewIssue(issue);

        // Assert
        result.Should().Contain("```csharp");
        result.Should().Contain("if (condition)");
        result.Should().Contain("DoSomething();");
        result.Should().Contain("```");
    }

    [Fact]
    public void FormatReviewIssue_WithDifferentCategories_ShouldShowCorrectCategory()
    {
        // Arrange & Act & Assert
        var categories = new[]
        {
            IssueCategory.Security,
            IssueCategory.Correctness,
            IssueCategory.Performance,
            IssueCategory.Style,
            IssueCategory.Docs,
            IssueCategory.Tests
        };

        foreach (var category in categories)
        {
            var issue = new ReviewIssue
            {
                Title = "Test",
                Severity = IssueSeverity.Error,
                Category = category,
                FilePath = "file.cs",
                Line = 1,
                Rationale = "Rationale",
                Recommendation = "Recommendation",
                FixExample = null,
                Fingerprint = $"fp_{category}"
            };

            var result = CommentFormatter.FormatReviewIssue(issue);
            result.Should().Contain(category.ToString());
        }
    }

    [Fact]
    public void FormatStateThread_WithMultipleIssues_ShouldIncludeAll()
    {
        // Arrange
        var issues = new List<ReviewIssue>
        {
            new() { Title = "Issue 1", Severity = IssueSeverity.Error, Category = IssueCategory.Correctness, FilePath = "file1.cs", Line = 10, Rationale = "R1", Recommendation = "Rec1", Fingerprint = "fp1" },
            new() { Title = "Issue 2", Severity = IssueSeverity.Warn, Category = IssueCategory.Style, FilePath = "file2.cs", Line = 20, Rationale = "R2", Recommendation = "Rec2", Fingerprint = "fp2" },
            new() { Title = "Issue 3", Severity = IssueSeverity.Info, Category = IssueCategory.Docs, FilePath = "file3.cs", Line = 30, Rationale = "R3", Recommendation = "Rec3", Fingerprint = "fp3" }
        };
        var result = new ReviewPlanResult(issues, 1, 2, 3);

        // Act
        var formatted = CommentFormatter.FormatStateThread(result);

        // Assert
        formatted.Should().Contain("fp1");
        formatted.Should().Contain("fp2");
        formatted.Should().Contain("fp3");
        formatted.Should().Contain("file1.cs");
        formatted.Should().Contain("file2.cs");
        formatted.Should().Contain("file3.cs");
    }
}
