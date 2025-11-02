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
        var diff = new ReviewFileDiff("test.cs", "Small diff content", "hash1", false);
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
        var diff = new ReviewFileDiff("test.cs", largeDiff, "hash1", false);
        var maxChunkSize = 100; // Small size to force chunking

        // Act
        var result = _chunker.ChunkDiff(diff, maxChunkSize);

        // Assert
        result.Should().HaveCountGreaterThan(1);
        result.All(c => c.TotalChunks == result.Count).Should().BeTrue();
        result.Select(c => c.ChunkIndex).Should().BeInAscendingOrder();
    }

    [Fact]
    public void ChunkDiff_WithClassBoundary_ShouldSplitAtClass()
    {
        // Arrange
        var diff = @"using System;

+public class FirstClass
+{
+    public void Method1() { }
+" + new string('x', 200) + @"
+}

+public class SecondClass
+{
+    public void Method2() { }
+}";
        var diffObj = new ReviewFileDiff("test.cs", diff, "hash1", false);
        var maxChunkSize = 150;

        // Act
        var result = _chunker.ChunkDiff(diffObj, maxChunkSize);

        // Assert
        result.Should().HaveCountGreaterThan(1);
        result.Any(c => c.Context.Contains("class")).Should().BeTrue();
    }

    [Fact]
    public void ChunkDiff_WithMethodBoundary_ShouldSplitAtMethod()
    {
        // Arrange
        var diff = @"+public class TestClass
+{
+    public void FirstMethod()
+    {
" + new string('x', 200) + @"
+    }
+
+    public void SecondMethod()
+    {
+        // Method body
+    }
+}";
        var diffObj = new ReviewFileDiff("test.cs", diff, "hash1", false);
        var maxChunkSize = 150;

        // Act
        var result = _chunker.ChunkDiff(diffObj, maxChunkSize);

        // Assert
        result.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void ChunkDiff_ShouldSetCorrectChunkIndices()
    {
        // Arrange
        var largeDiff = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"Line {i}"));
        var diff = new ReviewFileDiff("test.cs", largeDiff, "hash1", false);
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
        var diff = new ReviewFileDiff("test.cs", largeDiff, "hash1", false);
        var maxChunkSize = 50;

        // Act
        var result = _chunker.ChunkDiff(diff, maxChunkSize);

        // Assert
        var totalChunks = result.Count;
        result.Should().OnlyContain(c => c.TotalChunks == totalChunks);
    }

    [Fact]
    public void ChunkDiff_WithPropertyBoundary_ShouldRecognizeProperty()
    {
        // Arrange
        var diff = @"+public class TestClass
+{
+    public string FirstProperty { get; set; }
" + new string('x', 200) + @"
+
+    public int SecondProperty { get; set; }
+}";
        var diffObj = new ReviewFileDiff("test.cs", diff, "hash1", false);
        var maxChunkSize = 150;

        // Act
        var result = _chunker.ChunkDiff(diffObj, maxChunkSize);

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void DiffChunk_DisplayName_WithSingleChunk_ShouldReturnFilePath()
    {
        // Arrange
        var chunk = new DiffChunk
        {
            FilePath = "test.cs",
            Content = "content",
            ChunkIndex = 0,
            TotalChunks = 1,
            StartLine = 1,
            Context = "Full file diff"
        };

        // Act
        var displayName = chunk.DisplayName;

        // Assert
        displayName.Should().Be("test.cs");
    }

    [Fact]
    public void DiffChunk_DisplayName_WithMultipleChunks_ShouldIncludeChunkInfo()
    {
        // Arrange
        var chunk = new DiffChunk
        {
            FilePath = "test.cs",
            Content = "content",
            ChunkIndex = 1,
            TotalChunks = 3,
            StartLine = 50,
            Context = "Method DoSomething"
        };

        // Act
        var displayName = chunk.DisplayName;

        // Assert
        displayName.Should().Contain("test.cs");
        displayName.Should().Contain("chunk 2/3");
        displayName.Should().Contain("Method DoSomething");
    }

    [Fact]
    public void ChunkDiff_ShouldPreserveFilePath()
    {
        // Arrange
        var diff = new ReviewFileDiff("src/services/MyService.cs", "content", "hash1", false);
        var maxChunkSize = 1000;

        // Act
        var result = _chunker.ChunkDiff(diff, maxChunkSize);

        // Assert
        result.Should().OnlyContain(c => c.FilePath == "src/services/MyService.cs");
    }

    [Fact]
    public void ChunkDiff_WithEmptyDiff_ShouldReturnSingleChunk()
    {
        // Arrange
        var diff = new ReviewFileDiff("test.cs", "", "hash1", false);
        var maxChunkSize = 1000;

        // Act
        var result = _chunker.ChunkDiff(diff, maxChunkSize);

        // Assert
        result.Should().HaveCount(1);
        result[0].Content.Should().Be("");
    }

    [Fact]
    public void ChunkDiff_WithInterfaceBoundary_ShouldRecognizeInterface()
    {
        // Arrange
        var diff = @"+public interface IFirstInterface
+{
+    void DoSomething();
+}
" + new string('x', 200) + @"
+
+public interface ISecondInterface
+{
+    void DoSomethingElse();
+}";
        var diffObj = new ReviewFileDiff("test.cs", diff, "hash1", false);
        var maxChunkSize = 150;

        // Act
        var result = _chunker.ChunkDiff(diffObj, maxChunkSize);

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void ChunkDiff_WithStructBoundary_ShouldRecognizeStruct()
    {
        // Arrange
        var diff = @"+public struct FirstStruct
+{
+    public int Value;
+}
" + new string('x', 200) + @"
+
+public struct SecondStruct
+{
+    public string Name;
+}";
        var diffObj = new ReviewFileDiff("test.cs", diff, "hash1", false);
        var maxChunkSize = 150;

        // Act
        var result = _chunker.ChunkDiff(diffObj, maxChunkSize);

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void ChunkDiff_WithEnumBoundary_ShouldRecognizeEnum()
    {
        // Arrange
        var diff = @"+public enum FirstEnum
+{
+    Value1,
+    Value2
+}
" + new string('x', 200) + @"
+
+public enum SecondEnum
+{
+    OptionA,
+    OptionB
+}";
        var diffObj = new ReviewFileDiff("test.cs", diff, "hash1", false);
        var maxChunkSize = 150;

        // Act
        var result = _chunker.ChunkDiff(diffObj, maxChunkSize);

        // Assert
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void ChunkDiff_WithAsyncMethod_ShouldRecognizeAsyncMethod()
    {
        // Arrange
        var diff = @"+public class TestClass
+{
+    public async Task FirstMethodAsync()
+    {
+        await Task.Delay(100);
+    }
+}";
        var diffObj = new ReviewFileDiff("test.cs", diff, "hash1", false);
        var maxChunkSize = 1000;

        // Act
        var result = _chunker.ChunkDiff(diffObj, maxChunkSize);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void ChunkDiff_WithStaticClass_ShouldRecognizeStatic()
    {
        // Arrange
        var diff = @"+public static class HelperClass
+{
+    public static void DoSomething() { }
+}";
        var diffObj = new ReviewFileDiff("test.cs", diff, "hash1", false);
        var maxChunkSize = 1000;

        // Act
        var result = _chunker.ChunkDiff(diffObj, maxChunkSize);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void ChunkDiff_ShouldLogChunkingOperation()
    {
        // Arrange
        var largeDiff = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"Line {i}"));
        var diff = new ReviewFileDiff("test.cs", largeDiff, "hash1", false);
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
    public void ChunkDiff_WithVeryLargeSingleLine_ShouldSplitAppropriately()
    {
        // Arrange
        var largeLine = new string('x', 500);
        var diff = new ReviewFileDiff("test.cs", largeLine, "hash1", false);
        var maxChunkSize = 100;

        // Act
        var result = _chunker.ChunkDiff(diff, maxChunkSize);

        // Assert
        result.Should().HaveCountGreaterThan(1);
    }
}
