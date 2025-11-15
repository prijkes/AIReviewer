using Quaaly.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Quaaly.Infrastructure.Utils;

/// <summary>
/// Loads and merges configuration from settings.ini and environment variables.
/// Uses Microsoft.Extensions.Configuration.Binder for automatic type conversion and binding.
/// </summary>
public static class SettingsLoader
{
    /// <summary>
    /// Loads configuration from settings.ini and environment variables.
    /// </summary>
    /// <param name="logger">Logger for diagnostic messages.</param>
    /// <param name="iniPath">Path to settings.ini file. Defaults to "settings.ini" in current directory.</param>
    /// <returns>Populated ReviewerOptions instance.</returns>
    /// <exception cref="FileNotFoundException">Thrown when settings.ini is not found.</exception>
    public static ReviewerOptions Load(ILogger? logger = null, string iniPath = "settings.ini")
    {
        // Verify settings.ini exists
        if (!File.Exists(iniPath))
        {
            throw new FileNotFoundException(
                $"settings.ini not found at: {Path.GetFullPath(iniPath)}\n" +
                "This file is required for Quaaly configuration.\n" +
                "See README.md for setup instructions.");
        }

        logger?.LogInformation("Loading configuration from {IniPath}", Path.GetFullPath(iniPath));

        // Build configuration from INI file
        IConfiguration config;

        try
        {
            config = new ConfigurationBuilder()
                .AddIniFile(iniPath, optional: false, reloadOnChange: false)
                .Build();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse settings.ini: {ex.Message}", ex);
        }

        // Create options instance
        var options = new ReviewerOptions();

        try
        {
            // Automatically bind all INI sections to the options object!
            // This will:
            // - Bind [AI] section to options.AI
            // - Bind [FunctionCalling] section to options.FunctionCalling
            // - Bind [Review] section to options.Review
            // - Bind [Files] section to options.Files
            // - Bind [Language] section to options.Language
            // - Automatically parse all primitive types (int, double, bool, string)
            // - Automatically use TypeConverter for Size types
            config.Bind(options);

            logger?.LogInformation("Successfully loaded static settings from {IniPath}", iniPath);

            // Log parsed size values for debugging
            logger?.LogDebug("Parsed Files:MaxFileBytes = {Value} ({Bytes} bytes)",
                config["Files:MaxFileBytes"], options.Files.MaxFileBytes.Bytes);
            logger?.LogDebug("Parsed Files:MaxDiffBytes = {Value} ({Bytes} bytes)",
                config["Files:MaxDiffBytes"], options.Files.MaxDiffBytes.Bytes);
            logger?.LogDebug("Parsed Files:MaxPromptDiffBytes = {Value} ({Bytes} bytes)",
                config["Files:MaxPromptDiffBytes"], options.Files.MaxPromptDiffBytes.Bytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to bind settings.ini to configuration objects: {ex.Message}\n" +
                "Ensure all required values are present and in the correct format.", ex);
        }

        // Load required dynamic values from environment
        LoadDynamicFromEnvironment(options, logger);

        return options;
    }

    /// <summary>
    /// Load required dynamic values from environment variables.
    /// These MUST come from environment (not INI).
    /// </summary>
    private static void LoadDynamicFromEnvironment(ReviewerOptions options, ILogger? logger)
    {
        logger?.LogDebug("Loading dynamic configuration from environment variables...");

        // Azure DevOps (required)
        options.AdoCollectionUrl = GetRequiredEnvVar("ADO_COLLECTION_URL", logger);
        options.AdoProject = GetRequiredEnvVar("ADO_PROJECT", logger);
        options.AdoAccessToken = GetRequiredEnvVar("ADO_ACCESS_TOKEN", logger);

        // Repo (one of these required)
        options.AdoRepoName = Environment.GetEnvironmentVariable("ADO_REPO_NAME");
        options.AdoRepoId = Environment.GetEnvironmentVariable("ADO_REPO_ID");

        if (string.IsNullOrWhiteSpace(options.AdoRepoName) && string.IsNullOrWhiteSpace(options.AdoRepoId))
        {
            throw new InvalidOperationException("Either ADO_REPO_NAME or ADO_REPO_ID environment variable must be set.");
        }

        // PR ID (optional - can be inferred)
        var prIdStr = Environment.GetEnvironmentVariable("ADO_PR_ID");
        if (!string.IsNullOrWhiteSpace(prIdStr) && int.TryParse(prIdStr, out var prId))
        {
            options.AdoPullRequestId = prId;
            logger?.LogDebug("Loaded ADO_PR_ID = {PrId}", prId);
        }

        // Build source version (optional)
        options.BuildSourceVersion = Environment.GetEnvironmentVariable("BUILD_SOURCE_VERSION");

        // Azure AI (required)
        options.AiFoundryEndpoint = GetRequiredEnvVar("AI_FOUNDRY_ENDPOINT", logger);
        options.AiFoundryApiKey = GetRequiredEnvVar("AI_FOUNDRY_API_KEY", logger);

        // Local repo path (required)
        options.LocalRepoPath = GetRequiredEnvVar("LOCAL_REPO_PATH", logger);

        logger?.LogInformation("Successfully loaded dynamic configuration from environment variables");
    }

    /// <summary>
    /// Gets a required environment variable or throws.
    /// </summary>
    private static string GetRequiredEnvVar(string name, ILogger? logger)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Required environment variable not set: {name}\n" +
                "See PIPELINE.md for required environment variables.");
        }

        logger?.LogDebug("Loaded {VarName} from environment", name);
        return value;
    }
}
