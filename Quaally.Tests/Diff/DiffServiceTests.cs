using Quaally.Infrastructure.AzureDevOps;
using Quaally.Infrastructure.AzureDevOps.Models;
using Quaally.Infrastructure.Diff;
using Quaally.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace Quaally.Tests.Diff;

public class DiffServiceTests
{
    private readonly Mock<ILogger<DiffService>> _loggerMock;
    private readonly Mock<IAdoSdkClient> _adoClientMock;
    private readonly Mock<IOptionsMonitor<ReviewerOptions>> _optionsMock;
    private readonly ReviewerOptions _options;
    private readonly DiffService _diffService;

    public DiffServiceTests()
    {
        _loggerMock = new Mock<ILogger<DiffService>>();
        _adoClientMock = new Mock<IAdoSdkClient>();
        _optionsMock = new Mock<IOptionsMonitor<ReviewerOptions>>();

        _options = new ReviewerOptions
        {
            MaxDiffBytes = 10000
        };

        _optionsMock.Setup(x => x.CurrentValue).Returns(_options);
        _diffService = new DiffService(_loggerMock.Object, _adoClientMock.Object, _optionsMock.Object);
    }

    [Fact]
    public async Task GetDiffsAsync_WithValidChanges_ShouldReturnDiffs()
    {
        // Arrange
        var pr = CreateTestPullRequest();
        var iteration = new GitPullRequestIteration { Id = 1 };

        var changes = new GitPullRequestIterationChanges
        {
            ChangeEntries =
            [
                new()
                {
                    Item = new GitItem
                    {
                        Path = "/src/test.cs",
                        GitObjectType = GitObjectType.Blob
                    }
                }
            ]
        };

        _adoClientMock
            .Setup(x => x.GetIterationChangesAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(changes);

        _adoClientMock
            .Setup(x => x.GetFileDiffAsync(
                "/src/test.cs",
                "basecommit",
                "targetcommit",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("+added line\n-removed line");

        // Act
        var result = await _diffService.GetDiffsAsync(pr, iteration, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Path.Should().Be("/src/test.cs");
        result[0].DiffText.Should().Contain("+added line");
        result[0].IsBinary.Should().BeFalse();
    }

    [Fact]
    public async Task GetDiffsAsync_WithNoBlobChanges_ShouldSkipNonBlobs()
    {
        // Arrange
        var pr = CreateTestPullRequest();
        var iteration = new GitPullRequestIteration { Id = 1 };

        var changes = new GitPullRequestIterationChanges
        {
            ChangeEntries =
            [
                new()
                {
                    Item = new GitItem
                    {
                        Path = "/src/folder",
                        GitObjectType = GitObjectType.Tree // Directory, not blob
                    }
                }
            ]
        };

        _adoClientMock
            .Setup(x => x.GetIterationChangesAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(changes);

        // Act
        var result = await _diffService.GetDiffsAsync(pr, iteration, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDiffsAsync_WithEmptyDiff_ShouldSkipFile()
    {
        // Arrange
        var pr = CreateTestPullRequest();
        var iteration = new GitPullRequestIteration { Id = 1 };

        var changes = new GitPullRequestIterationChanges
        {
            ChangeEntries =
            [
                new()
                {
                    Item = new GitItem
                    {
                        Path = "/src/test.cs",
                        GitObjectType = GitObjectType.Blob
                    }
                }
            ]
        };

        _adoClientMock
            .Setup(x => x.GetIterationChangesAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(changes);

        _adoClientMock
            .Setup(x => x.GetFileDiffAsync(
                "/src/test.cs",
                "basecommit",
                "targetcommit",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _diffService.GetDiffsAsync(pr, iteration, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDiffsAsync_WithLargeDiff_ShouldTruncate()
    {
        // Arrange
        var pr = CreateTestPullRequest();
        var iteration = new GitPullRequestIteration { Id = 1 };
        var largeDiff = new string('x', 15000); // Exceeds MaxDiffBytes (10000)

        var changes = new GitPullRequestIterationChanges
        {
            ChangeEntries =
            [
                new()
                {
                    Item = new GitItem
                    {
                        Path = "/src/large.cs",
                        GitObjectType = GitObjectType.Blob
                    }
                }
            ]
        };

        _adoClientMock
            .Setup(x => x.GetIterationChangesAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(changes);

        _adoClientMock
            .Setup(x => x.GetFileDiffAsync(
                "/src/large.cs",
                "basecommit",
                "targetcommit",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(largeDiff);

        // Act
        var result = await _diffService.GetDiffsAsync(pr, iteration, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].DiffText.Length.Should().Be(10000);
    }

    [Fact]
    public async Task GetDiffsAsync_WithMultipleFiles_ShouldReturnAll()
    {
        // Arrange
        var pr = CreateTestPullRequest();
        var iteration = new GitPullRequestIteration { Id = 1 };

        var changes = new GitPullRequestIterationChanges
        {
            ChangeEntries =
            [
                new()
                {
                    Item = new GitItem
                    {
                        Path = "/src/file1.cs",
                        GitObjectType = GitObjectType.Blob
                    }
                },
                new()
                {
                    Item = new GitItem
                    {
                        Path = "/src/file2.cs",
                        GitObjectType = GitObjectType.Blob
                    }
                }
            ]
        };

        _adoClientMock
            .Setup(x => x.GetIterationChangesAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(changes);

        _adoClientMock
            .Setup(x => x.GetFileDiffAsync(
                "/src/file1.cs",
                "basecommit",
                "targetcommit",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("+line1");

        _adoClientMock
            .Setup(x => x.GetFileDiffAsync(
                "/src/file2.cs",
                "basecommit",
                "targetcommit",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("+line2");

        // Act
        var result = await _diffService.GetDiffsAsync(pr, iteration, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Path.Should().Be("/src/file1.cs");
        result[1].Path.Should().Be("/src/file2.cs");
    }

    [Fact]
    public async Task GetDiffsAsync_WithNullIteration_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var pr = CreateTestPullRequest();
        var iteration = new GitPullRequestIteration { Id = null };

        // Act
        var act = async () => await _diffService.GetDiffsAsync(pr, iteration, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Iteration ID is required");
    }

    [Fact]
    public async Task GetDiffsAsync_WithMissingCommits_ShouldReturnEmptyList()
    {
        // Arrange
        var pr = new PullRequestContext(
            new GitPullRequest
            {
                PullRequestId = 123,
                LastMergeTargetCommit = null,
                LastMergeSourceCommit = null
            },
            new GitRepository { Id = Guid.NewGuid() },
            [],
            new GitPullRequestIteration { Id = 1 }
        );
        var iteration = new GitPullRequestIteration { Id = 1 };

        // Act
        var result = await _diffService.GetDiffsAsync(pr, iteration, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDiffsAsync_WithNullChangeEntries_ShouldReturnEmptyList()
    {
        // Arrange
        var pr = CreateTestPullRequest();
        var iteration = new GitPullRequestIteration { Id = 1 };

        var changes = new GitPullRequestIterationChanges
        {
            ChangeEntries = null
        };

        _adoClientMock
            .Setup(x => x.GetIterationChangesAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(changes);

        // Act
        var result = await _diffService.GetDiffsAsync(pr, iteration, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDiffsAsync_WithNullPath_ShouldSkipEntry()
    {
        // Arrange
        var pr = CreateTestPullRequest();
        var iteration = new GitPullRequestIteration { Id = 1 };

        var changes = new GitPullRequestIterationChanges
        {
            ChangeEntries =
            [
                new()
                {
                    Item = new GitItem
                    {
                        Path = null,
                        GitObjectType = GitObjectType.Blob
                    }
                }
            ]
        };

        _adoClientMock
            .Setup(x => x.GetIterationChangesAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(changes);

        // Act
        var result = await _diffService.GetDiffsAsync(pr, iteration, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDiffsAsync_ShouldGenerateUniqueFileHashes()
    {
        // Arrange
        var pr = CreateTestPullRequest();
        var iteration = new GitPullRequestIteration { Id = 1 };

        var changes = new GitPullRequestIterationChanges
        {
            ChangeEntries =
            [
                new()
                {
                    Item = new GitItem
                    {
                        Path = "/src/file1.cs",
                        GitObjectType = GitObjectType.Blob
                    }
                },
                new()
                {
                    Item = new GitItem
                    {
                        Path = "/src/file2.cs",
                        GitObjectType = GitObjectType.Blob
                    }
                }
            ]
        };

        _adoClientMock
            .Setup(x => x.GetIterationChangesAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(changes);

        _adoClientMock
            .Setup(x => x.GetFileDiffAsync(
                It.IsAny<string>(),
                "basecommit",
                "targetcommit",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("+different content");

        // Act
        var result = await _diffService.GetDiffsAsync(pr, iteration, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].FileHash.Should().NotBe(result[1].FileHash);
    }

    [Fact]
    public async Task GetDiffsAsync_ShouldLogTotalBytes()
    {
        // Arrange
        var pr = CreateTestPullRequest();
        var iteration = new GitPullRequestIteration { Id = 1 };

        var changes = new GitPullRequestIterationChanges
        {
            ChangeEntries =
            [
                new()
                {
                    Item = new GitItem
                    {
                        Path = "/src/test.cs",
                        GitObjectType = GitObjectType.Blob
                    }
                }
            ]
        };

        _adoClientMock
            .Setup(x => x.GetIterationChangesAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(changes);

        _adoClientMock
            .Setup(x => x.GetFileDiffAsync(
                "/src/test.cs",
                "basecommit",
                "targetcommit",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("+test");

        // Act
        await _diffService.GetDiffsAsync(pr, iteration, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Prepared")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private static PullRequestContext CreateTestPullRequest()
    {
        var pr = new GitPullRequest
        {
            PullRequestId = 123,
            LastMergeTargetCommit = new GitCommitRef { CommitId = "basecommit" },
            LastMergeSourceCommit = new GitCommitRef { CommitId = "targetcommit" }
        };

        return new PullRequestContext(
            pr,
            new GitRepository { Id = Guid.NewGuid() },
            [],
            new GitPullRequestIteration { Id = 1 }
        );
    }
}
