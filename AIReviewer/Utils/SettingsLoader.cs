using AIReviewer.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIReviewer.Utils;

/// <summary>
/// Loads and merges configuration from settings.ini and environment variables.
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
                "This file is required for AIReviewer configuration.\n" +
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

        // Create options and populate from INI
        var options = new ReviewerOptions();
        
        // [AI] section
        options.AiFoundryDeployment = GetIniValue(config, "AI", "Deployment", logger);
        options.AiTemperature = GetIniDouble(config, "AI", "Temperature", logger);
        options.AiMaxTokens = GetIniInt(config, "AI", "MaxTokens", logger);
        
        // [FunctionCalling] section
        options.EnableFunctionCalling = GetIniBool(config, "FunctionCalling", "Enabled", logger);
        options.MaxFunctionCalls = GetIniInt(config, "FunctionCalling", "MaxCalls", logger);
        
        // [Review] section
        options.DryRun = GetIniBool(config, "Review", "DryRun", logger);
        options.ReviewScope = GetIniValue(config, "Review", "Scope", logger);
        options.WarnBudget = GetIniInt(config, "Review", "WarnBudget", logger);
        options.PolicyPath = GetIniValue(config, "Review", "PolicyPath", logger);
        
        // [Files] section
        options.MaxFilesToReview = GetIniInt(config, "Files", "MaxFilesToReview", logger);
        options.MaxIssuesPerFile = GetIniInt(config, "Files", "MaxIssuesPerFile", logger);
        options.MaxFileBytes = GetIniSize(config, "Files", "MaxFileBytes", logger);
        options.MaxDiffBytes = GetIniSize(config, "Files", "MaxDiffBytes", logger);
        options.MaxPromptDiffBytes = GetIniSize(config, "Files", "MaxPromptDiffBytes", logger);
        options.MaxCommitMessagesToReview = GetIniInt(config, "Files", "MaxCommitMessagesToReview", logger);
        
        // [Language] section
        options.JapaneseDetectionThreshold = GetIniDouble(config, "Language", "JapaneseDetectionThreshold", logger);

        logger?.LogInformation("Successfully loaded static settings from {IniPath}", iniPath);
        
        // Override with environment variables
        OverrideFromEnvironment(options, logger);
        
        // Load required dynamic values from environment
        LoadDynamicFromEnvironment(options, logger);
        
        return options;
    }

    /// <summary>
    /// Gets a string value from INI file.
    /// </summary>
    private static string GetIniValue(IConfiguration config, string section, string key, ILogger? logger)
    {
        var configKey = $"{section}:{key}";
        var value = config[configKey];
        
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required value in settings.ini: [{section}] {key}");
        }
        
        logger?.LogDebug("Loaded [{Section}] {Key} = {Value}", section, key, value);
        return value;
    }

    /// <summary>
    /// Gets an integer value from INI file.
    /// </summary>
    private static int GetIniInt(IConfiguration config, string section, string key, ILogger? logger)
    {
        var value = GetIniValue(config, section, key, logger);
        if (!int.TryParse(value, out var result))
        {
            throw new InvalidOperationException($"Invalid integer value in settings.ini: [{section}] {key} = {value}");
        }
        return result;
    }

    /// <summary>
    /// Gets a double value from INI file.
    /// </summary>
    private static double GetIniDouble(IConfiguration config, string section, string key, ILogger? logger)
    {
        var value = GetIniValue(config, section, key, logger);
        if (!double.TryParse(value, out var result))
        {
            throw new InvalidOperationException($"Invalid decimal value in settings.ini: [{section}] {key} = {value}");
        }
        return result;
    }

    /// <summary>
    /// Gets a boolean value from INI file.
    /// </summary>
    private static bool GetIniBool(IConfiguration config, string section, string key, ILogger? logger)
    {
        var value = GetIniValue(config, section, key, logger);
        if (!bool.TryParse(value, out var result))
        {
            throw new InvalidOperationException($"Invalid boolean value in settings.ini: [{section}] {key} = {value}. Use 'true' or 'false'.");
        }
        return result;
    }

    /// <summary>
    /// Gets a size value from INI file (supports human-readable formats like "200KB", "1.5GB").
    /// </summary>
    private static int GetIniSize(IConfiguration config, string section, string key, ILogger? logger)
    {
        var value = GetIniValue(config, section, key, logger);
        try
        {
            var bytes = SizeParser.ParseToBytes(value);
            logger?.LogDebug("Parsed [{Section}] {Key} = {Value} → {Bytes} bytes ({Formatted})", 
                section, key, value, bytes, SizeParser.FormatBytes(bytes));
            return bytes;
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"Invalid size value in settings.ini: [{section}] {key} = {value}. {ex.Message}");
        }
    }

    /// <summary>
    /// Override INI values with environment variables (if set).
    /// Pattern: [Section] Key → SECTION_KEY
    /// </summary>
    private static void OverrideFromEnvironment(ReviewerOptions options, ILogger? logger)
    {
        logger?.LogDebug("Checking for environment variable overrides...");
        
        // AI section
        OverrideIfSet("AI_DEPLOYMENT", v => options.AiFoundryDeployment = v, logger);
        OverrideIfSet("AI_TEMPERATURE", v => options.AiTemperature = double.Parse(v), logger);
        OverrideIfSet("AI_MAXTOKENS", v => options.AiMaxTokens = int.Parse(v), logger);
        
        // FunctionCalling section
        OverrideIfSet("FUNCTIONCALLING_ENABLED", v => options.EnableFunctionCalling = bool.Parse(v), logger);
        OverrideIfSet("FUNCTIONCALLING_MAXCALLS", v => options.MaxFunctionCalls = int.Parse(v), logger);
        
        // Review section
        OverrideIfSet("REVIEW_DRYRUN", v => options.DryRun = bool.Parse(v), logger);
        OverrideIfSet("REVIEW_SCOPE", v => options.ReviewScope = v, logger);
        OverrideIfSet("REVIEW_WARNBUDGET", v => options.WarnBudget = int.Parse(v), logger);
        OverrideIfSet("REVIEW_POLICYPATH", v => options.PolicyPath = v, logger);
        
        // Files section
        OverrideIfSet("FILES_MAXFILESTOREVIEW", v => options.MaxFilesToReview = int.Parse(v), logger);
        OverrideIfSet("FILES_MAXISSUESPERFILE", v => options.MaxIssuesPerFile = int.Parse(v), logger);
        OverrideIfSet("FILES_MAXFILEBYTES", v => options.MaxFileBytes = int.Parse(v), logger);
        OverrideIfSet("FILES_MAXDIFFBYTES", v => options.MaxDiffBytes = int.Parse(v), logger);
        OverrideIfSet("FILES_MAXPROMPTDIFFBYTES", v => options.MaxPromptDiffBytes = int.Parse(v), logger);
        OverrideIfSet("FILES_MAXCOMMITMESSAGESTOREVIEW", v => options.MaxCommitMessagesToReview = int.Parse(v), logger);
        
        // Language section
        OverrideIfSet("LANGUAGE_JAPANESEDETECTIONTHRESHOLD", v => options.JapaneseDetectionThreshold = double.Parse(v), logger);
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

    /// <summary>
    /// Override a value if environment variable is set.
    /// </summary>
    private static void OverrideIfSet(string envVar, Action<string> setter, ILogger? logger)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(value))
        {
            try
            {
                setter(value);
                logger?.LogInformation("Overridden from environment: {EnvVar} = {Value}", envVar, value);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Invalid value for environment variable {envVar}: {value}", ex);
            }
        }
    }
}
