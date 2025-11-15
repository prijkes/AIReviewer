using Microsoft.Extensions.Logging;

namespace Quaaly.Infrastructure.Options;

/// <summary>
/// Validates ReviewerOptions configuration at startup and provides clear error messages.
/// </summary>
public static class ReviewerOptionsValidator
{
    /// <summary>
    /// Validates all configuration options and returns a list of validation errors.
    /// </summary>
    public static List<string> Validate(ReviewerOptions options)
    {
        var errors = new List<string>();

        // Azure DevOps Configuration
        if (string.IsNullOrWhiteSpace(options.AdoCollectionUrl))
            errors.Add("ADO_COLLECTION_URL is required but not set");

        if (string.IsNullOrWhiteSpace(options.AdoProject))
            errors.Add("ADO_PROJECT is required but not set");

        if (string.IsNullOrWhiteSpace(options.AdoRepoId) && string.IsNullOrWhiteSpace(options.AdoRepoName))
            errors.Add("Either ADO_REPO_ID or ADO_REPO_NAME must be set");

        if (string.IsNullOrWhiteSpace(options.AdoAccessToken))
            errors.Add("ADO_ACCESS_TOKEN is required but not set");

        if (!string.IsNullOrWhiteSpace(options.AdoCollectionUrl))
        {
            if (!Uri.TryCreate(options.AdoCollectionUrl, UriKind.Absolute, out _))
                errors.Add($"ADO_COLLECTION_URL '{options.AdoCollectionUrl}' is not a valid URL");
        }

        // AI Configuration
        if (string.IsNullOrWhiteSpace(options.AiFoundryEndpoint))
            errors.Add("AI_FOUNDRY_ENDPOINT is required but not set");

        if (!string.IsNullOrWhiteSpace(options.AiFoundryEndpoint))
        {
            if (!Uri.TryCreate(options.AiFoundryEndpoint, UriKind.Absolute, out _))
                errors.Add($"AI_FOUNDRY_ENDPOINT '{options.AiFoundryEndpoint}' is not a valid URL");
        }

        if (string.IsNullOrWhiteSpace(options.AiFoundryDeployment))
            errors.Add("AI_FOUNDRY_DEPLOYMENT is required but not set");

        if (string.IsNullOrWhiteSpace(options.AiFoundryApiKey))
            errors.Add("AI_FOUNDRY_API_KEY is required but not set");

        // AI Parameters
        if (options.AiTemperature < 0.0 || options.AiTemperature > 1.0)
            errors.Add($"AI_TEMPERATURE must be between 0.0 and 1.0 (current: {options.AiTemperature})");

        if (options.AiMaxTokens <= 0)
            errors.Add($"AI_MAX_TOKENS must be greater than 0 (current: {options.AiMaxTokens})");

        if (options.AiMaxTokens > 128000)
            errors.Add($"AI_MAX_TOKENS exceeds reasonable limit (current: {options.AiMaxTokens}, max recommended: 128000)");

        // Review Settings
        if (options.WarnBudget < 0)
            errors.Add($"WARN_BUDGET must be non-negative (current: {options.WarnBudget})");

        if (options.MaxFilesToReview <= 0)
            errors.Add($"MAX_FILES_TO_REVIEW must be greater than 0 (current: {options.MaxFilesToReview})");

        if (options.MaxIssuesPerFile <= 0)
            errors.Add($"MAX_ISSUES_PER_FILE must be greater than 0 (current: {options.MaxIssuesPerFile})");

        if (options.MaxFileBytes <= 0)
            errors.Add($"MAX_FILE_BYTES must be greater than 0 (current: {options.MaxFileBytes})");

        if (options.MaxDiffBytes <= 0)
            errors.Add($"MAX_DIFF_BYTES must be greater than 0 (current: {options.MaxDiffBytes})");

        if (options.MaxPromptDiffBytes <= 0)
            errors.Add($"MAX_PROMPT_DIFF_BYTES must be greater than 0 (current: {options.MaxPromptDiffBytes})");

        if (options.MaxCommitMessagesToReview <= 0)
            errors.Add($"MAX_COMMIT_MESSAGES_TO_REVIEW must be greater than 0 (current: {options.MaxCommitMessagesToReview})");

        // Language Detection
        if (options.JapaneseDetectionThreshold < 0.0 || options.JapaneseDetectionThreshold > 1.0)
            errors.Add($"JAPANESE_DETECTION_THRESHOLD must be between 0.0 and 1.0 (current: {options.JapaneseDetectionThreshold})");

        // Policy Path
        if (string.IsNullOrWhiteSpace(options.PolicyPath))
            errors.Add("POLICY_PATH is required but not set");

        if (!string.IsNullOrWhiteSpace(options.PolicyPath))
        {
            var policyPath = Path.IsPathFullyQualified(options.PolicyPath)
                ? options.PolicyPath
                : Path.Combine(AppContext.BaseDirectory, options.PolicyPath);

            if (!File.Exists(policyPath))
                errors.Add($"POLICY_PATH file does not exist: {policyPath}");
        }

        // Local Repository Path
        if (string.IsNullOrWhiteSpace(options.LocalRepoPath))
            errors.Add("LOCAL_REPO_PATH is required but not set");

        if (!string.IsNullOrWhiteSpace(options.LocalRepoPath))
        {
            if (!Directory.Exists(options.LocalRepoPath))
                errors.Add($"LOCAL_REPO_PATH directory does not exist: {options.LocalRepoPath}");
        }

        // Function Calling
        if (options.MaxFunctionCalls <= 0)
            errors.Add($"MAX_FUNCTION_CALLS must be greater than 0 (current: {options.MaxFunctionCalls})");

        return errors;
    }

    /// <summary>
    /// Validates options and logs errors. Returns true if valid, false otherwise.
    /// </summary>
    public static bool ValidateAndLog(ReviewerOptions options, ILogger logger)
    {
        var errors = Validate(options);

        if (errors.Count > 0)
        {
            logger.LogError(
                "Configuration validation failed with {ErrorCount} error(s):\n  - {Errors}",
                errors.Count,
                string.Join("\n  - ", errors));
            return false;
        }

        logger.LogInformation("Configuration validation passed");
        return true;
    }

    /// <summary>
    /// Logs current configuration values for debugging.
    /// </summary>
    public static void LogConfiguration(ReviewerOptions options, ILogger logger)
    {
        logger.LogInformation(
            @"Current Configuration:
Azure DevOps:
  Collection URL: {AdoCollectionUrl}
  Project: {AdoProject}
  Repository: {AdoRepo}
  Pull Request: {AdoPrId}
  
AI Provider:
  Endpoint: {AiEndpoint}
  Deployment: {AiDeployment}
  Temperature: {AiTemp}
  Max Tokens: {AiMaxTokens}
  Function Calling: {FunctionCalling}
  
Review Settings:
  Dry Run: {DryRun}
  Max Files: {MaxFiles}
  Max Issues/File: {MaxIssues}
  Warn Budget: {WarnBudget}
  Policy Path: {PolicyPath}
  
Language:
  Japanese Detection Threshold: {JapaneseThreshold}",
            options.AdoCollectionUrl,
            options.AdoProject,
            options.AdoRepoId ?? options.AdoRepoName ?? "Not set",
            options.AdoPullRequestId?.ToString() ?? "Will be inferred",
            options.AiFoundryEndpoint,
            options.AiFoundryDeployment,
            options.AiTemperature,
            options.AiMaxTokens,
            options.EnableFunctionCalling,
            options.DryRun,
            options.MaxFilesToReview,
            options.MaxIssuesPerFile,
            options.WarnBudget,
            options.PolicyPath,
            options.JapaneseDetectionThreshold);
    }
}
