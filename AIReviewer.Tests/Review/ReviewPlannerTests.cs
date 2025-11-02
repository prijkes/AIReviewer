using AIReviewer.AI;
using AIReviewer.AzureDevOps.Models;
using AIReviewer.Diff;
using AIReviewer.Options;
using AIReviewer.Review;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AIReviewer.Tests.Review;

public class ReviewPlannerTests
{
    private readonly Mock<ILogger<ReviewPlanner>> _loggerMock;
    private readonly Mock<IAiClient> _aiClientMock;
    private readonly Mock<IOptionsMonitor<ReviewerOptions>> _optionsMock;
    private readonly ReviewerOptions _options;

    public ReviewPlannerTests()
    {
        _loggerMock = new Mock<ILogger<ReviewPlanner>>();
        _aiClientMock = new Mock<IAiClient>();
        _optionsMock = new Mock<IOptionsMonitor<ReviewerOptions>>();
        
        _options = new ReviewerOptions
        {
            MaxFilesToReview = 50,
            MaxIssuesPerFile = 5,
            MaxDiffBytes = 500000,
            WarnBudget = 3,
            JapaneseDetectionThreshold = 0.3
        };
        
        _optionsMock.Setup(x => x.CurrentValue).Returns(_options);
    }

    private ReviewPlanner CreatePlanner()
    {
        // Create a null context retriever for testing since we don't use function calling in these tests
        return new ReviewPlanner(
            _loggerMock.Object,
            _aiClientMock.Object,
            null!,  // Context retriever not needed for these tests
            _optionsMock.Object);
    }

    [Fact]
    public async Task PlanAsync_WithNoIssues_ShouldApprove()
    {
        // Arrange
        var planner = CreatePlanner();
        var pr = CreateTestPullRequest();
        var iteration = new GitPullRequestIteration { Id = 1 };
        var diffs = new List<ReviewFileDiff>
        {
            new("test.cs", "diff content", "hash1", false)
        };
        var policy = "Test policy";

        _aiClientMock
            .Setup(x => x.ReviewAsync(It.IsAny<string>(), It.IsAny<ReviewFileDiff>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiReviewResponse([]));

        _aiClientMock
            .Setup(x => x.ReviewPullRequestMetadataAsync(It.IsAny<string>(), It.IsAny<PullRequestMetadata>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiReviewResponse([]));

        // Act
        var result = await planner.PlanAsync(pr, iteration, diffs, policy, CancellationToken.None);

        // Assert
        result.ShouldApprove.Should().BeTrue();
        result.ErrorCount.Should().Be(0);
        result.WarningCount.Should().Be(0);
    }

    [Fact]
    public async Task PlanAsync_WithErrors_ShouldNotApprove()
    {
        // Arrange
        var planner = CreatePlanner();
        var pr = CreateTestPullRequest();
        var iteration = new GitPullRequestIteration { Id = 1 };
        var diffs = new List<ReviewFileDiff>
        {
            new("test.cs", "diff content", "hash1", false)
        };
        var policy = "Test policy";

        var issues = new List<AiIssue>
        {
            new("E1", "Error 1", IssueSeverity.Error, IssueCategory.Security, "test.cs", 10, "Rationale", "Fix it", null)
        };

        _aiClientMock
            .Setup(x => x.ReviewAsync(It.IsAny<string>(), It.IsAny<ReviewFileDiff>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiReviewResponse(issues));

        _aiClientMock
            .Setup(x => x.ReviewPullRequestMetadataAsync(It.IsAny<string>(), It.IsAny<PullRequestMetadata>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiReviewResponse([]));

        // Act
        var result = await planner.PlanAsync(pr, iteration, diffs, policy, CancellationToken.None);

        // Assert
        result.ShouldApprove.Should().BeFalse();
        result.ErrorCount.Should().Be(1);
        result.WarningCount.Should().Be(0);
    }

    [Fact]
    public async Task PlanAsync_WithWarningsUnderBudget_ShouldApprove()
    {
        // Arrange
        var planner = CreatePlanner();
        var pr = CreateTestPullRequest();
        var iteration = new GitPullRequestIteration { Id = 1 };
        var diffs = new List<ReviewFileDiff>
        {
            new("test.cs", "diff content", "hash1", false)
        };
        var policy = "Test policy";

        var issues = new List<AiIssue>
        {
            new("W1", "Warning 1", IssueSeverity.Warn, IssueCategory.Style, "test.cs", 10, "Rationale", "Fix it", null),
            new("W2", "Warning 2", IssueSeverity.Warn, IssueCategory.Style, "test.cs", 20, "Rationale", "Fix it", null)
        };

        _aiClientMock
            .Setup(x => x.ReviewAsync(It.IsAny<string>(), It.IsAny<ReviewFileDiff>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiReviewResponse(issues));

        _aiClientMock
            .Setup(x => x.ReviewPullRequestMetadataAsync(It.IsAny<string>(), It.IsAny<PullRequestMetadata>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiReviewResponse([]));

        // Act
        var result = await planner.PlanAsync(pr, iteration, diffs, policy, CancellationToken.None);

        // Assert (WarnBudget = 3, we have 2 warnings)
        result.ShouldApprove.Should().BeTrue();
        result.ErrorCount.Should().Be(0);
        result.WarningCount.Should().Be(2);
    }

    [Fact]
    public async Task PlanAsync_WithWarningsOverBudget_ShouldNotApprove()
    {
        // Arrange
        var planner = CreatePlanner();
        var pr = CreateTestPullRequest();
        var iteration = new GitPullRequestIteration { Id = 1 };
        var diffs = new List<ReviewFileDiff>
        {
            new("test.cs", "diff content", "hash1", false)
        };
        var policy = "Test policy";

        var issues = new List<AiIssue>
        {
            new("W1", "Warning 1", IssueSeverity.Warn, IssueCategory.Style, "test.cs", 10, "Rationale", "Fix it", null),
            new("W2", "Warning 2", IssueSeverity.Warn, IssueCategory.Style, "test.cs", 20, "Rationale", "Fix it", null),
            new("W3", "Warning 3", IssueSeverity.Warn, IssueCategory.Style, "test.cs", 30, "Rationale", "Fix it", null),
            new("W4", "Warning 4", IssueSeverity.Warn, IssueCategory.Style, "test.cs", 40, "Rationale", "Fix it", null)
        };

        _aiClientMock
            .Setup(x => x.ReviewAsync(It.IsAny<string>(), It.IsAny<ReviewFileDiff>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiReviewResponse(issues));

        _aiClientMock
            .Setup(x => x.ReviewPullRequestMetadataAsync(It.IsAny<string>(), It.IsAny<PullRequestMetadata>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiReviewResponse([]));

        // Act
        var result = await planner.PlanAsync(pr, iteration, diffs, policy, CancellationToken.None);

        // Assert (WarnBudget = 3, we have 4 warnings)
        result.ShouldApprove.Should().BeFalse();
        result.ErrorCount.Should().Be(0);
        result.WarningCount.Should().Be(4);
    }

    [Fact]
    public async Task PlanAsync_WithTooManyFiles_ShouldOnlyReviewMaxFiles()
    {
        // Arrange
        var planner = CreatePlanner();
        var pr = CreateTestPullRequest();
        var iteration = new GitPullRequestIteration { Id = 1 };
        var diffs = Enumerable.Range(1, 60)
            .Select(i => new ReviewFileDiff($"file{i}.cs", "diff", $"hash{i}", false))
            .ToList();
        var policy = "Test policy";

        _aiClientMock
            .Setup(x => x.ReviewAsync(It.IsAny<string>(), It.IsAny<ReviewFileDiff>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiReviewResponse([]));

        _aiClientMock
            .Setup(x => x.ReviewPullRequestMetadataAsync(It.IsAny<string>(), It.IsAny<PullRequestMetadata>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiReviewResponse([]));

        // Act
        await planner.PlanAsync(pr, iteration, diffs, policy, CancellationToken.None);

        // Assert - Should only review MaxFilesToReview (50) files
        _aiClientMock.Verify(
            x => x.ReviewAsync(It.IsAny<string>(), It.IsAny<ReviewFileDiff>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(50));
    }

    private static PullRequestContext CreateTestPullRequest()
    {
        var pr = new GitPullRequest
        {
            PullRequestId = 123,
            Title = "Test PR",
            Description = "Test description"
        };

        var commits = new[]
        {
            new GitCommitRef { Comment = "Commit 1" },
            new GitCommitRef { Comment = "Commit 2" }
        };

        return new PullRequestContext(
            pr,
            new GitRepository { Id = Guid.NewGuid() },
            commits,
            new GitPullRequestIteration { Id = 1 }
        );
    }
}
