using AIReviewer.Utils;

namespace AIReviewer.Tests.Utils;

public class DotEnvParserTests : IDisposable
{
    private readonly List<string> _testFiles = [];
    private readonly Dictionary<string, string?> _originalEnvVars = [];

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        // Clean up test files
        foreach (var file in _testFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        // Restore original environment variables
        foreach (var kvp in _originalEnvVars)
        {
            Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
        }
    }

    private string CreateTestEnvFile(string content)
    {
        var tempFile = Path.GetTempFileName();
        _testFiles.Add(tempFile);
        File.WriteAllText(tempFile, content);
        return tempFile;
    }

    private void SaveEnvVar(string key)
    {
        if (!_originalEnvVars.ContainsKey(key))
        {
            _originalEnvVars[key] = Environment.GetEnvironmentVariable(key);
        }
    }

    [Fact]
    public void Parse_WithNonExistentFile_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var result = DotEnvParser.Parse(nonExistentFile);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WithSimpleKeyValue_ShouldParseCorrectly()
    {
        // Arrange
        SaveEnvVar("TEST_KEY");
        var envFile = CreateTestEnvFile("TEST_KEY=test_value");

        // Act
        var result = DotEnvParser.Parse(envFile);

        // Assert
        result.Should().ContainKey("TEST_KEY");
        result["TEST_KEY"].Should().Be("test_value");
        Environment.GetEnvironmentVariable("TEST_KEY").Should().Be("test_value");
    }

    [Fact]
    public void Parse_WithDoubleQuotedValue_ShouldRemoveQuotes()
    {
        // Arrange
        SaveEnvVar("QUOTED_KEY");
        var envFile = CreateTestEnvFile("QUOTED_KEY=\"quoted value\"");

        // Act
        var result = DotEnvParser.Parse(envFile);

        // Assert
        result["QUOTED_KEY"].Should().Be("quoted value");
    }

    [Fact]
    public void Parse_WithSingleQuotedValue_ShouldRemoveQuotes()
    {
        // Arrange
        SaveEnvVar("SINGLE_QUOTED");
        var envFile = CreateTestEnvFile("SINGLE_QUOTED='single quoted'");

        // Act
        var result = DotEnvParser.Parse(envFile);

        // Assert
        result["SINGLE_QUOTED"].Should().Be("single quoted");
    }

    [Fact]
    public void Parse_WithExportPrefix_ShouldParseCorrectly()
    {
        // Arrange
        SaveEnvVar("EXPORT_KEY");
        var envFile = CreateTestEnvFile("export EXPORT_KEY=exported_value");

        // Act
        var result = DotEnvParser.Parse(envFile);

        // Assert
        result["EXPORT_KEY"].Should().Be("exported_value");
    }

    [Fact]
    public void Parse_WithComments_ShouldSkipComments()
    {
        // Arrange
        SaveEnvVar("REAL_KEY");
        var envFile = CreateTestEnvFile(@"# This is a comment
REAL_KEY=real_value
# Another comment");

        // Act
        var result = DotEnvParser.Parse(envFile);

        // Assert
        result.Should().HaveCount(1);
        result["REAL_KEY"].Should().Be("real_value");
    }

    [Fact]
    public void Parse_WithEmptyLines_ShouldSkipEmptyLines()
    {
        // Arrange
        SaveEnvVar("KEY1");
        SaveEnvVar("KEY2");
        var envFile = CreateTestEnvFile(@"KEY1=value1

KEY2=value2

");

        // Act
        var result = DotEnvParser.Parse(envFile);

        // Assert
        result.Should().HaveCount(2);
        result["KEY1"].Should().Be("value1");
        result["KEY2"].Should().Be("value2");
    }

    [Fact]
    public void Parse_WithWhitespace_ShouldTrimWhitespace()
    {
        // Arrange
        SaveEnvVar("SPACE_KEY");
        var envFile = CreateTestEnvFile("  SPACE_KEY  =  space value  ");

        // Act
        var result = DotEnvParser.Parse(envFile);

        // Assert
        result["SPACE_KEY"].Should().Be("space value");
    }

    [Fact]
    public void Parse_WithEqualsInValue_ShouldIncludeInValue()
    {
        // Arrange
        SaveEnvVar("URL_KEY");
        var envFile = CreateTestEnvFile("URL_KEY=https://example.com?param=value");

        // Act
        var result = DotEnvParser.Parse(envFile);

        // Assert
        result["URL_KEY"].Should().Be("https://example.com?param=value");
    }

    [Fact]
    public void Parse_WithNoEqualsSign_ShouldSkipLine()
    {
        // Arrange
        SaveEnvVar("VALID_KEY");
        var envFile = CreateTestEnvFile(@"INVALID_LINE_NO_EQUALS
VALID_KEY=valid_value");

        // Act
        var result = DotEnvParser.Parse(envFile);

        // Assert
        result.Should().HaveCount(1);
        result.Should().ContainKey("VALID_KEY");
    }

    [Fact]
    public void Parse_WithEmptyValue_ShouldSetEmptyString()
    {
        // Arrange
        SaveEnvVar("EMPTY_KEY");
        var envFile = CreateTestEnvFile("EMPTY_KEY=");

        // Act
        var result = DotEnvParser.Parse(envFile);

        // Assert
        result["EMPTY_KEY"].Should().BeEmpty();
    }

    [Fact]
    public void Parse_WithCaseInsensitiveKeys_ShouldHandleCorrectly()
    {
        // Arrange
        SaveEnvVar("MYKEY");
        var envFile = CreateTestEnvFile("MYKEY=value1");

        // Act
        var result = DotEnvParser.Parse(envFile);

        // Assert
        result.Should().ContainKey("mykey");
        result.Should().ContainKey("MYKEY");
        result.Should().ContainKey("MyKey");
    }

    [Fact]
    public void Parse_WithMultipleVariables_ShouldParseAll()
    {
        // Arrange
        SaveEnvVar("VAR1");
        SaveEnvVar("VAR2");
        SaveEnvVar("VAR3");
        var envFile = CreateTestEnvFile(@"VAR1=value1
VAR2=""value2""
export VAR3='value3'");

        // Act
        var result = DotEnvParser.Parse(envFile);

        // Assert
        result.Should().HaveCount(3);
        result["VAR1"].Should().Be("value1");
        result["VAR2"].Should().Be("value2");
        result["VAR3"].Should().Be("value3");
    }
}
