using System.ComponentModel.DataAnnotations;

namespace AIReviewer.Options;

/// <summary>
/// Configuration options for the AI code reviewer application.
/// These settings control Azure DevOps connection, AI model parameters, and review behavior.
/// </summary>
public sealed class ReviewerOptions
{
    /// <summary>
    /// Azure DevOps collection URL (e.g., https://dev.azure.com/organization).
    /// </summary>
    [Required]
    public string AdoCollectionUrl { get; set; } = string.Empty;

    /// <summary>
    /// Azure DevOps project name.
    /// </summary>
    [Required]
    public string AdoProject { get; set; } = string.Empty;

    /// <summary>
    /// Azure DevOps repository ID (GUID). Either this or AdoRepoName must be specified.
    /// </summary>
    public string? AdoRepoId { get; set; }

    /// <summary>
    /// Azure DevOps repository name. Either this or AdoRepoId must be specified.
    /// </summary>
    public string? AdoRepoName { get; set; }

    /// <summary>
    /// Pull request ID to review. If not specified, will be inferred from BuildSourceVersion.
    /// </summary>
    public int? AdoPullRequestId { get; set; }

    /// <summary>
    /// Personal access token (PAT) for authenticating with Azure DevOps.
    /// </summary>
    [Required]
    public string AdoAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Azure AI Foundry endpoint URL for the OpenAI service.
    /// </summary>
    [Required]
    public string AiFoundryEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI deployment/model name to use for reviews.
    /// Must be configured in appsettings.json or via environment variable.
    /// </summary>
    [Required]
    public string AiFoundryDeployment { get; set; } = string.Empty;

    /// <summary>
    /// API key for authenticating with Azure AI Foundry.
    /// </summary>
    [Required]
    public string AiFoundryApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Temperature parameter for AI model (0.0-1.0). Lower values are more deterministic.
    /// Must be configured in appsettings.json or via environment variable.
    /// </summary>
    [Required]
    public double AiTemperature { get; set; }

    /// <summary>
    /// Maximum number of tokens for AI response.
    /// Must be configured in appsettings.json or via environment variable.
    /// </summary>
    [Required]
    public int AiMaxTokens { get; set; }

    /// <summary>
    /// Enables OpenAI function calling to allow the AI to retrieve additional context during review.
    /// When enabled, the AI can call functions to get full file contents, search the codebase, etc.
    /// This improves review quality but increases API costs and latency.
    /// Must be configured in appsettings.json or via environment variable.
    /// </summary>
    [Required]
    public bool EnableFunctionCalling { get; set; }

    /// <summary>
    /// Maximum number of function calls the AI can make during a single review.
    /// This prevents infinite loops and controls API costs.
    /// Must be configured in appsettings.json or via environment variable.
    /// </summary>
    [Required]
    public int MaxFunctionCalls { get; set; }

    /// <summary>
    /// Threshold ratio (0.0-1.0) for detecting Japanese language in PR descriptions.
    /// If the ratio of Japanese characters to non-whitespace characters exceeds this threshold,
    /// reviews will be conducted in Japanese.
    /// Must be configured in appsettings.json or via environment variable.
    /// </summary>
    [Required]
    public double JapaneseDetectionThreshold { get; set; }

    /// <summary>
    /// When true, performs review without posting comments or approvals to Azure DevOps.
    /// Must be configured in appsettings.json or via environment variable.
    /// </summary>
    [Required]
    public bool DryRun { get; set; }

    /// <summary>
    /// Scope of files to review.
    /// Must be configured in appsettings.json or via environment variable.
    /// </summary>
    [Required]
    public string ReviewScope { get; set; } = string.Empty;

    /// <summary>
    /// Maximum file size in bytes to review. Larger files are skipped.
    /// Must be configured in appsettings.json or via environment variable.
    /// </summary>
    [Required]
    public int MaxFileBytes { get; set; }

    /// <summary>
    /// Maximum diff size in bytes to send to AI. Larger diffs are truncated.
    /// Must be configured in appsettings.json or via environment variable.
    /// </summary>
    [Required]
    public int MaxDiffBytes { get; set; }

    /// <summary>
    /// Maximum number of warnings before rejecting approval.
    /// Must be configured in appsettings.json or via environment variable.
    /// </summary>
    [Required]
    public int WarnBudget { get; set; }

    /// <summary>
    /// Maximum number of files to review in a single PR.
    /// Must be configured in appsettings.json or via environment variable.
    /// </summary>
    [Required]
    public int MaxFilesToReview { get; set; }

    /// <summary>
    /// Maximum number of issues to report per file.
    /// Must be configured in appsettings.json or via environment variable.
    /// </summary>
    [Required]
    public int MaxIssuesPerFile { get; set; }

    /// <summary>
    /// Maximum number of commit messages to include in metadata review.
    /// Must be configured in appsettings.json or via environment variable.
    /// </summary>
    [Required]
    public int MaxCommitMessagesToReview { get; set; }

    /// <summary>
    /// Maximum diff size in bytes to include in AI prompt. Larger diffs are truncated.
    /// Must be configured in appsettings.json or via environment variable.
    /// </summary>
    [Required]
    public int MaxPromptDiffBytes { get; set; }

    /// <summary>
    /// Path to the review policy markdown file.
    /// Must be configured in appsettings.json or via environment variable.
    /// </summary>
    [Required]
    public string PolicyPath { get; set; } = string.Empty;

    /// <summary>
    /// Build source version (commit SHA) for inferring the pull request when AdoPullRequestId is not specified.
    /// </summary>
    public string? BuildSourceVersion { get; set; }

    /// <summary>
    /// Local repository path for accessing files directly from the filesystem instead of Azure DevOps API.
    /// When running in Azure Pipelines, this should be set to $(Build.SourcesDirectory).
    /// This dramatically reduces API calls and avoids rate limiting for large codebases.
    /// REQUIRED: All git file operations will use local filesystem.
    /// </summary>
    [Required]
    public string LocalRepoPath { get; set; } = string.Empty;

    /// <summary>
    /// Normalizes option values (e.g., path separators).
    /// All defaults must be configured in appsettings.json.
    /// </summary>
    public void Normalize()
    {
        // Normalize LocalRepoPath to use forward slashes and remove trailing slash
        if (!string.IsNullOrWhiteSpace(LocalRepoPath))
        {
            LocalRepoPath = LocalRepoPath.Replace('\\', '/').TrimEnd('/');
        }
    }
}
