using AIReviewer.Diff;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Tests.Diff;

public class DiffChunkerTests
{
    private readonly Mock<ILogger<DiffChunker>> _loggerMock;
    private readonly DiffChunker _chunker;

    public DiffChunkerTests()
    {
        _loggerMock = new Mock<ILogger<DiffChunker>>();
        _chunker = new DiffChunker(_loggerMock.Object);
    }

    [Fact]
    public void ChunkDiff_WithSmallDiff_ShouldReturnSingleChunk()
    {
        // Arrange
        var diff = new ReviewFileDiff("test.cs", "Small diff content", "hash1", false, false);
        var maxChunkSize = 1000;

        // Act
        var result = _chunker.ChunkDiff(diff, maxChunkSize);

        // Assert
        result.Should().HaveCount(1);
        result[0].FilePath.Should().Be("test.cs");
        result[0].Content.Should().Be("Small diff content");
        result[0].ChunkIndex.Should().Be(0);
        result[0].TotalChunks.Should().Be(1);
        result[0].Context.Should().Be("Full file diff");
    }

    [Fact]
    public void ChunkDiff_WithLargeDiff_ShouldSplitIntoMultipleChunks()
    {
        // Arrange
        var largeDiff = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"Line {i}"));
        var diff = new ReviewFileDiff("test.cs", largeDiff, "hash1", false, false);
        var maxChunkSize = 100; // Small size to force chunking

        // Act
        var result = _chunker.ChunkDiff(diff, maxChunkSize);

        // Assert
        result.Should().HaveCountGreaterThan(1);
        result.All(c => c.TotalChunks == result.Count).Should().BeTrue();
        result.Select(c => c.ChunkIndex).Should().BeInAscendingOrder();
    }


    [Fact]
    public void ChunkDiff_ShouldSetCorrectChunkIndices()
    {
        // Arrange
        var largeDiff = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"Line {i}"));
        var diff = new ReviewFileDiff("test.cs", largeDiff, "hash1", false, false);
        var maxChunkSize = 50;

        // Act
        var result = _chunker.ChunkDiff(diff, maxChunkSize);

        // Assert
        for (int i = 0; i < result.Count; i++)
        {
            result[i].ChunkIndex.Should().Be(i);
        }
    }

    [Fact]
    public void ChunkDiff_ShouldSetTotalChunksCorrectly()
    {
        // Arrange
        var largeDiff = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"Line {i}"));
        var diff = new ReviewFileDiff("test.cs", largeDiff, "hash1", false, false);
        var maxChunkSize = 50;

        // Act
        var result = _chunker.ChunkDiff(diff, maxChunkSize);

        // Assert
        var totalChunks = result.Count;
        result.Should().OnlyContain(c => c.TotalChunks == totalChunks);
    }


    [Fact]
    public void DiffChunk_DisplayName_WithSingleChunk_ShouldReturnFilePath()
    {
        // Arrange
        var diff = new ReviewFileDiff("test.cs", "small content", "hash1", false, false);

        // Act
        var result = _chunker.ChunkDiff(diff, 1000);

        // Assert
        result.Should().HaveCount(1);
        result[0].DisplayName.Should().Be("test.cs");
    }

    [Fact]
    public void DiffChunk_DisplayName_WithMultipleChunks_ShouldIncludeChunkInfo()
    {
        // Arrange
        var largeDiff = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"Line {i}"));
        var diff = new ReviewFileDiff("test.cs", largeDiff, "hash1", false, false);

        // Act
        var result = _chunker.ChunkDiff(diff, 50);

        // Assert
        result.Should().HaveCountGreaterThan(1);
        result[1].DisplayName.Should().Contain("test.cs");
        result[1].DisplayName.Should().Contain("chunk");
    }

    [Fact]
    public void ChunkDiff_WithDiffHunkBoundary_ShouldSplitAtHunk()
    {
        // Arrange
        var diffText = @"@@ -1,5 +1,5 @@
 line1
 line2
+added line
 line3
" + new string('x', 200) + @"
@@ -10,5 +10,7 @@ function foo()
 more content
+another addition
 end";
        var diff = new ReviewFileDiff("test.cs", diffText, "hash123", false, false);

        // Act
        var result = _chunker.ChunkDiff(diff, 250);

        // Assert
        result.Should().HaveCountGreaterThan(1);
        // Should split at diff hunk boundaries
        result.Any(c => c.Context.Contains("Lines starting at")).Should().BeTrue();
    }

    [Fact]
    public void ChunkDiff_WithEmptyDiff_ShouldReturnSingleChunk()
    {
        // Arrange
        var diff = new ReviewFileDiff("test.cs", "", "hash1", false, false);
        var maxChunkSize = 1000;

        // Act
        var result = _chunker.ChunkDiff(diff, maxChunkSize);

        // Assert
        result.Should().HaveCount(1);
        result[0].Content.Should().Be("");
    }


    [Fact]
    public void ChunkDiff_ShouldLogChunkingOperation()
    {
        // Arrange
        var largeDiff = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"Line {i}"));
        var diff = new ReviewFileDiff("test.cs", largeDiff, "hash1", false, false);
        var maxChunkSize = 50;

        // Act
        _chunker.ChunkDiff(diff, maxChunkSize);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Split")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void ChunkDiff_WithVeryLargeDiff_ShouldSplitAppropriately()
    {
        // Arrange - create a diff with content that exceeds max size
        var lines = Enumerable.Range(1, 20).Select(i => $"+Line {i} with some content");
        var largeDiff = string.Join("\n", lines);
        var diff = new ReviewFileDiff("test.cs", largeDiff, "hash1", false, false);
        var maxChunkSize = 100;

        // Act
        var result = _chunker.ChunkDiff(diff, maxChunkSize);

        // Assert
        result.Should().HaveCountGreaterThan(1);
        result.All(c => c.Content.Length <= maxChunkSize * 1.5).Should().BeTrue(); // Allow some overflow
    }
}
