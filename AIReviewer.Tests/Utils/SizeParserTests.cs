using AIReviewer.Utils;

namespace AIReviewer.Tests.Utils;

public class SizeParserTests
{
    [Theory]
    [InlineData("1000", 1000)]
    [InlineData("1024", 1024)]
    [InlineData("500", 500)]
    public void ParseToBytes_WithPlainNumber_ShouldReturnBytes(string input, int expected)
    {
        // Act
        var result = SizeParser.ParseToBytes(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1KB", 1024)]
    [InlineData("2KB", 2048)]
    [InlineData("100KB", 102400)]
    public void ParseToBytes_WithKilobytes_ShouldReturnCorrectBytes(string input, int expected)
    {
        // Act
        var result = SizeParser.ParseToBytes(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1MB", 1048576)]
    [InlineData("2MB", 2097152)]
    [InlineData("10MB", 10485760)]
    public void ParseToBytes_WithMegabytes_ShouldReturnCorrectBytes(string input, int expected)
    {
        // Act
        var result = SizeParser.ParseToBytes(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1GB", 1073741824)]
    [InlineData("2GB", 2147483648L)] // Will be capped due to int.MaxValue
    public void ParseToBytes_WithGigabytes_ShouldReturnCorrectBytes(string input, long expectedLong)
    {
        // Act & Assert
        if (expectedLong > int.MaxValue)
        {
            var act = () => SizeParser.ParseToBytes(input);
            act.Should().Throw<FormatException>()
                .WithMessage("*Size too large*");
        }
        else
        {
            var result = SizeParser.ParseToBytes(input);
            result.Should().Be((int)expectedLong);
        }
    }

    [Theory]
    [InlineData("1.5KB", 1536)]
    [InlineData("2.5MB", 2621440)]
    [InlineData("0.5KB", 512)]
    public void ParseToBytes_WithDecimalValues_ShouldReturnCorrectBytes(string input, int expected)
    {
        // Act
        var result = SizeParser.ParseToBytes(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1kb")]
    [InlineData("1Kb")]
    [InlineData("1kB")]
    [InlineData("1mb")]
    [InlineData("1MB")]
    public void ParseToBytes_WithDifferentCasing_ShouldBeCaseInsensitive(string input)
    {
        // Act
        var result = SizeParser.ParseToBytes(input);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("  200KB  ", 204800)]
    [InlineData("100MB ", 104857600)]
    [InlineData(" 1KB", 1024)]
    public void ParseToBytes_WithWhitespace_ShouldTrimAndParse(string input, int expected)
    {
        // Act
        var result = SizeParser.ParseToBytes(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseToBytes_WithEmptyOrWhitespace_ShouldThrowFormatException(string input)
    {
        // Act
        var act = () => SizeParser.ParseToBytes(input);

        // Assert
        act.Should().Throw<FormatException>()
            .WithMessage("Size string cannot be empty");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("notanumber")]
    [InlineData("XYZ")]
    public void ParseToBytes_WithInvalidFormat_ShouldThrowFormatException(string input)
    {
        // Act
        var act = () => SizeParser.ParseToBytes(input);

        // Assert
        act.Should().Throw<FormatException>()
            .WithMessage("*Invalid size format*");
    }

    [Theory]
    [InlineData("100TB")]
    [InlineData("100PB")]
    [InlineData("100ZB")]
    public void ParseToBytes_WithUnknownUnit_ShouldThrowFormatException(string input)
    {
        // Act
        var act = () => SizeParser.ParseToBytes(input);

        // Assert
        act.Should().Throw<FormatException>()
            .WithMessage("*Unknown size unit*");
    }

    [Theory]
    [InlineData(0L, "0B")]
    [InlineData(500L, "500B")]
    [InlineData(1023L, "1023B")]
    [InlineData(1024L, "1KB")]
    [InlineData(1536L, "1.5KB")]
    [InlineData(1048576L, "1MB")]
    [InlineData(1572864L, "1.5MB")]
    [InlineData(1073741824L, "1GB")]
    [InlineData(1610612736L, "1.5GB")]
    public void FormatBytes_WithVariousSizes_ShouldFormatCorrectly(long bytes, string expected)
    {
        // Act
        var result = SizeParser.FormatBytes(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void FormatBytes_WithLargeNumber_ShouldFormatAsGB()
    {
        // Arrange
        var bytes = 5368709120L; // 5GB

        // Act
        var result = SizeParser.FormatBytes(bytes);

        // Assert
        result.Should().Be("5GB");
    }

    [Fact]
    public void FormatBytes_WithZero_ShouldReturnZeroBytes()
    {
        // Act
        var result = SizeParser.FormatBytes(0);

        // Assert
        result.Should().Be("0B");
    }

    [Theory]
    [InlineData("200KB")]
    [InlineData("1.5GB")]
    [InlineData("500MB")]
    public void ParseToBytes_RoundTrip_ShouldMaintainApproximateValue(string original)
    {
        // Act
        var bytes = SizeParser.ParseToBytes(original);
        var formatted = SizeParser.FormatBytes(bytes);
        var reparsed = SizeParser.ParseToBytes(formatted);

        // Assert
        reparsed.Should().Be(bytes);
    }

    [Fact]
    public void ParseToBytes_WithBUnit_ShouldParseCorrectly()
    {
        // Act
        var result = SizeParser.ParseToBytes("1024B");

        // Assert
        result.Should().Be(1024);
    }

    [Fact]
    public void ParseToBytes_WithMaxIntValue_ShouldSucceed()
    {
        // Arrange
        var maxBytes = int.MaxValue.ToString();

        // Act
        var result = SizeParser.ParseToBytes(maxBytes);

        // Assert
        result.Should().Be(int.MaxValue);
    }

    [Fact]
    public void ParseToBytes_ExceedingMaxInt_ShouldThrowFormatException()
    {
        // Arrange - 3GB exceeds int.MaxValue (2,147,483,647 bytes)
        var input = "3GB";

        // Act
        var act = () => SizeParser.ParseToBytes(input);

        // Assert
        act.Should().Throw<FormatException>()
            .WithMessage("*Size too large*");
    }
}
