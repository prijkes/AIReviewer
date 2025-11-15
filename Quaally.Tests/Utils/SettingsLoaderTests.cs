using Quaally.Options;
using Quaally.Utils;

namespace Quaally.Tests.Utils;

public class SettingsLoaderTests
{
    [Fact]
    public void Load_ValidSettingsFile_ShouldBindAllSections()
    {
        // Arrange - Create a temporary settings.ini file
        var tempIniPath = Path.GetTempFileName();
        File.WriteAllText(tempIniPath, @"
[AI]
Deployment = gpt-4
Temperature = 0.5
MaxTokens = 3000

[FunctionCalling]
Enabled = true
MaxCalls = 10

[Review]
DryRun = true
OnlyReviewIfRequiredReviewer = false
Scope = changed-files
WarnBudget = 5
PolicyPath = ./test-policy.md
PromptsBasePath = ./test-prompts

[Files]
MaxFilesToReview = 100
MaxIssuesPerFile = 10
MaxFileBytes = 500KB
MaxDiffBytes = 2MB
MaxPromptDiffBytes = 16KB
MaxCommitMessagesToReview = 20

[Language]
JapaneseDetectionThreshold = 0.4
");

        // Set required environment variables
        Environment.SetEnvironmentVariable("ADO_COLLECTION_URL", "https://dev.azure.com/test");
        Environment.SetEnvironmentVariable("ADO_PROJECT", "TestProject");
        Environment.SetEnvironmentVariable("ADO_REPO_NAME", "TestRepo");
        Environment.SetEnvironmentVariable("ADO_ACCESS_TOKEN", "test-token");
        Environment.SetEnvironmentVariable("AI_FOUNDRY_ENDPOINT", "https://test.openai.azure.com");
        Environment.SetEnvironmentVariable("AI_FOUNDRY_API_KEY", "test-key");
        Environment.SetEnvironmentVariable("LOCAL_REPO_PATH", "/test/repo");

        try
        {
            // Act
            var options = SettingsLoader.Load(logger: null, iniPath: tempIniPath);

            // Assert - AI section
            Assert.Equal("gpt-4", options.AI.Deployment);
            Assert.Equal(0.5, options.AI.Temperature);
            Assert.Equal(3000, options.AI.MaxTokens);

            // Assert - FunctionCalling section
            Assert.True(options.FunctionCalling.Enabled);
            Assert.Equal(10, options.FunctionCalling.MaxCalls);

            // Assert - Review section
            Assert.True(options.Review.DryRun);
            Assert.False(options.Review.OnlyReviewIfRequiredReviewer);
            Assert.Equal("changed-files", options.Review.Scope);
            Assert.Equal(5, options.Review.WarnBudget);
            Assert.Equal("./test-policy.md", options.Review.PolicyPath);
            Assert.Equal("./test-prompts", options.Review.PromptsBasePath);

            // Assert - Files section
            Assert.Equal(100, options.Files.MaxFilesToReview);
            Assert.Equal(10, options.Files.MaxIssuesPerFile);
            Assert.Equal(20, options.Files.MaxCommitMessagesToReview);

            // Assert - Size values are automatically parsed!
            Assert.Equal(512000, options.Files.MaxFileBytes.Bytes); // 500KB
            Assert.Equal(2097152, options.Files.MaxDiffBytes.Bytes); // 2MB
            Assert.Equal(16384, options.Files.MaxPromptDiffBytes.Bytes); // 16KB

            // Assert - Language section
            Assert.Equal(0.4, options.Language.JapaneseDetectionThreshold);

            // Assert - Backward compatibility properties still work
            Assert.Equal("gpt-4", options.AiFoundryDeployment);
            Assert.Equal(0.5, options.AiTemperature);
            Assert.Equal(3000, options.AiMaxTokens);
            Assert.True(options.EnableFunctionCalling);
            Assert.Equal(10, options.MaxFunctionCalls);
            Assert.True(options.DryRun);
            Assert.Equal(5, options.WarnBudget);
            Assert.Equal(512000, options.MaxFileBytes);
            Assert.Equal(2097152, options.MaxDiffBytes);
            Assert.Equal(16384, options.MaxPromptDiffBytes);

            // Assert - Environment variables loaded
            Assert.Equal("https://dev.azure.com/test", options.AdoCollectionUrl);
            Assert.Equal("TestProject", options.AdoProject);
            Assert.Equal("TestRepo", options.AdoRepoName);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempIniPath))
            {
                File.Delete(tempIniPath);
            }

            // Clear environment variables
            Environment.SetEnvironmentVariable("ADO_COLLECTION_URL", null);
            Environment.SetEnvironmentVariable("ADO_PROJECT", null);
            Environment.SetEnvironmentVariable("ADO_REPO_NAME", null);
            Environment.SetEnvironmentVariable("ADO_ACCESS_TOKEN", null);
            Environment.SetEnvironmentVariable("AI_FOUNDRY_ENDPOINT", null);
            Environment.SetEnvironmentVariable("AI_FOUNDRY_API_KEY", null);
            Environment.SetEnvironmentVariable("LOCAL_REPO_PATH", null);
        }
    }

    [Theory]
    [InlineData("200KB", 204800)]
    [InlineData("1MB", 1048576)]
    [InlineData("1.5GB", 1610612736)]
    [InlineData("8KB", 8192)]
    public void Size_TypeConverter_ShouldParseCorrectly(string input, int expectedBytes)
    {
        // Arrange & Act
        var size = new Size(input);

        // Assert
        Assert.Equal(expectedBytes, size.Bytes);
        Assert.Equal(expectedBytes, (int)size); // Implicit conversion
    }
}
