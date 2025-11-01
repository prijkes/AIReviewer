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
    /// Azure OpenAI deployment/model name to use for reviews. Default is "o4-mini".
    /// </summary>
    public string AiFoundryDeployment { get; set; } = "o4-mini";

    /// <summary>
    /// API key for authenticating with Azure AI Foundry.
    /// </summary>
    [Required]
    public string AiFoundryApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Temperature parameter for AI model (0.0-1.0). Lower values are more deterministic. Default is 0.2.
    /// </summary>
    public double AiTemperature { get; set; } = 0.2;

    /// <summary>
    /// Maximum number of tokens for AI response. Default is 2000.
    /// </summary>
    public int AiMaxTokens { get; set; } = 2000;

    /// <summary>
    /// Enables OpenAI function calling to allow the AI to retrieve additional context during review.
    /// When enabled, the AI can call functions to get full file contents, search the codebase, etc.
    /// This improves review quality but increases API costs and latency. Default is false.
    /// </summary>
    public bool EnableFunctionCalling { get; set; } = false;

    /// <summary>
    /// When true, performs review without posting comments or approvals to Azure DevOps. Default is false.
    /// </summary>
    public bool DryRun { get; set; } = false;

    /// <summary>
    /// Scope of files to review. Default is "changed-files".
    /// </summary>
    public string ReviewScope { get; set; } = "changed-files";

    /// <summary>
    /// Maximum file size in bytes to review. Larger files are skipped. Default is 200,000 bytes.
    /// </summary>
    public int MaxFileBytes { get; set; } = 200000;

    /// <summary>
    /// Maximum diff size in bytes to send to AI. Larger diffs are truncated. Default is 500,000 bytes.
    /// </summary>
    public int MaxDiffBytes { get; set; } = 500000;

    /// <summary>
    /// Maximum number of warnings before rejecting approval. Default is 3.
    /// </summary>
    public int WarnBudget { get; set; } = 3;

    /// <summary>
    /// Maximum number of files to review in a single PR. Default is 50.
    /// </summary>
    public int MaxFilesToReview { get; set; } = 50;

    /// <summary>
    /// Maximum number of issues to report per file. Default is 5.
    /// </summary>
    public int MaxIssuesPerFile { get; set; } = 5;

    /// <summary>
    /// Maximum number of commit messages to include in metadata review. Default is 10.
    /// </summary>
    public int MaxCommitMessagesToReview { get; set; } = 10;

    /// <summary>
    /// Maximum diff size in bytes to include in AI prompt. Larger diffs are truncated. Default is 8000 bytes.
    /// </summary>
    public int MaxPromptDiffBytes { get; set; } = 8000;

    /// <summary>
    /// Path to the review policy markdown file. Default is "./policy/review-policy.md".
    /// </summary>
    public string PolicyPath { get; set; } = "./policy/review-policy.md";

    /// <summary>
    /// Build source version (commit SHA) for inferring the pull request when AdoPullRequestId is not specified.
    /// </summary>
    public string? BuildSourceVersion { get; set; }

    /// <summary>
    /// Local repository path for accessing files directly from the filesystem instead of Azure DevOps API.
    /// When running in Azure Pipelines, this should be set to $(Build.SourcesDirectory).
    /// This dramatically reduces API calls and avoids rate limiting for large codebases.
    /// </summary>
    public string? LocalRepoPath { get; set; }

    /// <summary>
    /// Normalizes option values by applying default values where needed.
    /// </summary>
    public void Normalize()
    {
        ReviewScope = string.IsNullOrWhiteSpace(ReviewScope) ? "changed-files" : ReviewScope;
        AiFoundryDeployment = string.IsNullOrWhiteSpace(AiFoundryDeployment) ? "o4-mini" : AiFoundryDeployment;
        PolicyPath = string.IsNullOrWhiteSpace(PolicyPath) ? "./policy/review-policy.md" : PolicyPath;
        
        // Normalize LocalRepoPath to use forward slashes and remove trailing slash
        if (!string.IsNullOrWhiteSpace(LocalRepoPath))
        {
            LocalRepoPath = LocalRepoPath.Replace('\\', '/').TrimEnd('/');
        }
    }
}
