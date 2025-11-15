using Quaally.Options;
using Microsoft.Extensions.Logging;

namespace Quaally.Tests.Options;

public class ReviewerOptionsValidatorTests
{
    private readonly Mock<ILogger> _loggerMock;

    public ReviewerOptionsValidatorTests()
    {
        _loggerMock = new Mock<ILogger>();
    }

    private ReviewerOptions CreateValidOptions()
    {
        return new ReviewerOptions
        {
            AdoCollectionUrl = "https://dev.azure.com/myorg",
            AdoProject = "MyProject",
            AdoRepoId = "repo123",
            AdoAccessToken = "token123",
            AiFoundryEndpoint = "https://ai.azure.com",
            AiFoundryDeployment = "gpt-4",
            AiFoundryApiKey = "apikey123",
            AiTemperature = 0.7,
            AiMaxTokens = 4000,
            WarnBudget = 3,
            MaxFilesToReview = 50,
            MaxIssuesPerFile = 5,
            MaxFileBytes = 1000000,
            MaxDiffBytes = 500000,
            MaxPromptDiffBytes = 200000,
            MaxCommitMessagesToReview = 10,
            JapaneseDetectionThreshold = 0.3,
            PolicyPath = "policy.md",
            LocalRepoPath = ".",
            MaxFunctionCalls = 10
        };
    }

    [Fact]
    public void Validate_WithValidOptions_ShouldReturnNoErrors()
    {
        // Arrange
        var options = CreateValidOptions();

        // Create a temporary policy file for the test
        var policyPath = Path.Combine(AppContext.BaseDirectory, "policy.md");
        File.WriteAllText(policyPath, "# Test Policy");

        try
        {
            // Act
            var errors = ReviewerOptionsValidator.Validate(options);

            // Assert
            errors.Should().BeEmpty();
        }
        finally
        {
            // Cleanup
            if (File.Exists(policyPath))
                File.Delete(policyPath);
        }
    }

    [Fact]
    public void Validate_WithMissingAdoCollectionUrl_ShouldReturnError()
    {
        // Arrange
        var options = CreateValidOptions();
        options.AdoCollectionUrl = "";

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("ADO_COLLECTION_URL"));
    }

    [Fact]
    public void Validate_WithInvalidAdoCollectionUrl_ShouldReturnError()
    {
        // Arrange
        var options = CreateValidOptions();
        options.AdoCollectionUrl = "not a valid url";

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("ADO_COLLECTION_URL") && e.Contains("not a valid URL"));
    }

    [Fact]
    public void Validate_WithMissingAdoProject_ShouldReturnError()
    {
        // Arrange
        var options = CreateValidOptions();
        options.AdoProject = "";

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("ADO_PROJECT"));
    }

    [Fact]
    public void Validate_WithMissingBothRepoIdAndName_ShouldReturnError()
    {
        // Arrange
        var options = CreateValidOptions();
        options.AdoRepoId = "";
        options.AdoRepoName = "";

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("ADO_REPO_ID") && e.Contains("ADO_REPO_NAME"));
    }

    [Fact]
    public void Validate_WithRepoName_ShouldBeValid()
    {
        // Arrange
        var options = CreateValidOptions();
        options.AdoRepoId = "";
        options.AdoRepoName = "MyRepo";

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().NotContain(e => e.Contains("ADO_REPO"));
    }

    [Fact]
    public void Validate_WithMissingAccessToken_ShouldReturnError()
    {
        // Arrange
        var options = CreateValidOptions();
        options.AdoAccessToken = "";

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("ADO_ACCESS_TOKEN"));
    }

    [Fact]
    public void Validate_WithMissingAiEndpoint_ShouldReturnError()
    {
        // Arrange
        var options = CreateValidOptions();
        options.AiFoundryEndpoint = "";

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("AI_FOUNDRY_ENDPOINT"));
    }

    [Fact]
    public void Validate_WithInvalidAiEndpoint_ShouldReturnError()
    {
        // Arrange
        var options = CreateValidOptions();
        options.AiFoundryEndpoint = "not a valid url";

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("AI_FOUNDRY_ENDPOINT") && e.Contains("not a valid URL"));
    }

    [Fact]
    public void Validate_WithMissingDeployment_ShouldReturnError()
    {
        // Arrange
        var options = CreateValidOptions();
        options.AiFoundryDeployment = "";

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("AI_FOUNDRY_DEPLOYMENT"));
    }

    [Fact]
    public void Validate_WithMissingApiKey_ShouldReturnError()
    {
        // Arrange
        var options = CreateValidOptions();
        options.AiFoundryApiKey = "";

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("AI_FOUNDRY_API_KEY"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void Validate_WithInvalidTemperature_ShouldReturnError(double temperature)
    {
        // Arrange
        var options = CreateValidOptions();
        options.AiTemperature = temperature;

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("AI_TEMPERATURE"));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Validate_WithValidTemperature_ShouldBeValid(double temperature)
    {
        // Arrange
        var options = CreateValidOptions();
        options.AiTemperature = temperature;

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().NotContain(e => e.Contains("AI_TEMPERATURE"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidMaxTokens_ShouldReturnError(int tokens)
    {
        // Arrange
        var options = CreateValidOptions();
        options.AiMaxTokens = tokens;

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("AI_MAX_TOKENS"));
    }

    [Fact]
    public void Validate_WithExcessiveMaxTokens_ShouldReturnError()
    {
        // Arrange
        var options = CreateValidOptions();
        options.AiMaxTokens = 200000;

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("AI_MAX_TOKENS") && e.Contains("exceeds"));
    }

    [Fact]
    public void Validate_WithNegativeWarnBudget_ShouldReturnError()
    {
        // Arrange
        var options = CreateValidOptions();
        options.WarnBudget = -1;

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("WARN_BUDGET"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidMaxFiles_ShouldReturnError(int maxFiles)
    {
        // Arrange
        var options = CreateValidOptions();
        options.MaxFilesToReview = maxFiles;

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("MAX_FILES_TO_REVIEW"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidMaxIssues_ShouldReturnError(int maxIssues)
    {
        // Arrange
        var options = CreateValidOptions();
        options.MaxIssuesPerFile = maxIssues;

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("MAX_ISSUES_PER_FILE"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_WithInvalidJapaneseThreshold_ShouldReturnError(double threshold)
    {
        // Arrange
        var options = CreateValidOptions();
        options.JapaneseDetectionThreshold = threshold;

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("JAPANESE_DETECTION_THRESHOLD"));
    }

    [Fact]
    public void Validate_WithMissingPolicyPath_ShouldReturnError()
    {
        // Arrange
        var options = CreateValidOptions();
        options.PolicyPath = "";

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("POLICY_PATH"));
    }

    [Fact]
    public void Validate_WithMissingLocalRepoPath_ShouldReturnError()
    {
        // Arrange
        var options = CreateValidOptions();
        options.LocalRepoPath = "";

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("LOCAL_REPO_PATH"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidMaxFunctionCalls_ShouldReturnError(int maxCalls)
    {
        // Arrange
        var options = CreateValidOptions();
        options.MaxFunctionCalls = maxCalls;

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("MAX_FUNCTION_CALLS"));
    }

    [Fact]
    public void ValidateAndLog_WithValidOptions_ShouldReturnTrue()
    {
        // Arrange
        var options = CreateValidOptions();

        // Create a temporary policy file for the test
        var policyPath = Path.Combine(AppContext.BaseDirectory, "policy.md");
        File.WriteAllText(policyPath, "# Test Policy");

        try
        {
            // Act
            var result = ReviewerOptionsValidator.ValidateAndLog(options, _loggerMock.Object);

            // Assert
            result.Should().BeTrue();
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("validation passed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            // Cleanup
            if (File.Exists(policyPath))
                File.Delete(policyPath);
        }
    }

    [Fact]
    public void ValidateAndLog_WithInvalidOptions_ShouldReturnFalseAndLogErrors()
    {
        // Arrange
        var options = CreateValidOptions();
        options.AdoCollectionUrl = "";
        options.AdoProject = "";

        // Act
        var result = ReviewerOptionsValidator.ValidateAndLog(options, _loggerMock.Object);

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("validation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogConfiguration_ShouldLogAllSettings()
    {
        // Arrange
        var options = CreateValidOptions();

        // Act
        ReviewerOptionsValidator.LogConfiguration(options, _loggerMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Current Configuration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Validate_WithMultipleErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var options = new ReviewerOptions
        {
            AdoCollectionUrl = "",
            AdoProject = "",
            AdoAccessToken = "",
            AiFoundryEndpoint = "",
            AiFoundryDeployment = "",
            AiFoundryApiKey = "",
            PolicyPath = "",
            LocalRepoPath = ""
        };

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().HaveCountGreaterThan(5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidMaxDiffBytes_ShouldReturnError(int bytes)
    {
        // Arrange
        var options = CreateValidOptions();
        options.MaxDiffBytes = bytes;

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("MAX_DIFF_BYTES"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidMaxPromptDiffBytes_ShouldReturnError(int bytes)
    {
        // Arrange
        var options = CreateValidOptions();
        options.MaxPromptDiffBytes = bytes;

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("MAX_PROMPT_DIFF_BYTES"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidMaxCommitMessages_ShouldReturnError(int messages)
    {
        // Arrange
        var options = CreateValidOptions();
        options.MaxCommitMessagesToReview = messages;

        // Act
        var errors = ReviewerOptionsValidator.Validate(options);

        // Assert
        errors.Should().Contain(e => e.Contains("MAX_COMMIT_MESSAGES_TO_REVIEW"));
    }
}
