using System.ComponentModel.DataAnnotations;
using Quaally.Core.Enums;

namespace Quaally.Infrastructure.Options;

/// <summary>
/// Configuration options for the AI code reviewer application.
/// These settings control Azure DevOps connection, AI model parameters, and review behavior.
/// Structure mirrors the settings.ini file sections for automatic binding.
/// </summary>
public sealed class ReviewerOptions
{
    // ========================================================================
    // Dynamic values - loaded from environment variables (credentials, etc.)
    // ========================================================================

    /// <summary>
    /// Azure DevOps collection URL (e.g., https://dev.azure.com/organization).
    /// Loaded from ADO_COLLECTION_URL environment variable.
    /// </summary>
    [Required]
    public string AdoCollectionUrl { get; set; } = string.Empty;

    /// <summary>
    /// Azure DevOps project name.
    /// Loaded from ADO_PROJECT environment variable.
    /// </summary>
    [Required]
    public string AdoProject { get; set; } = string.Empty;

    /// <summary>
    /// Azure DevOps repository ID (GUID). Either this or AdoRepoName must be specified.
    /// Loaded from ADO_REPO_ID environment variable.
    /// </summary>
    public string? AdoRepoId { get; set; }

    /// <summary>
    /// Azure DevOps repository name. Either this or AdoRepoId must be specified.
    /// Loaded from ADO_REPO_NAME environment variable.
    /// </summary>
    public string? AdoRepoName { get; set; }

    /// <summary>
    /// Pull request ID to review. If not specified, will be inferred from BuildSourceVersion.
    /// Loaded from ADO_PR_ID environment variable.
    /// </summary>
    public int? AdoPullRequestId { get; set; }

    /// <summary>
    /// Personal access token (PAT) for authenticating with Azure DevOps.
    /// Loaded from ADO_ACCESS_TOKEN environment variable.
    /// </summary>
    [Required]
    public string AdoAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Azure AI Foundry endpoint URL for the OpenAI service.
    /// Loaded from AI_FOUNDRY_ENDPOINT environment variable.
    /// </summary>
    [Required]
    public string AiFoundryEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// API key for authenticating with Azure AI Foundry.
    /// Loaded from AI_FOUNDRY_API_KEY environment variable.
    /// </summary>
    [Required]
    public string AiFoundryApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Build source version (commit SHA) for inferring the pull request when AdoPullRequestId is not specified.
    /// Loaded from BUILD_SOURCE_VERSION environment variable.
    /// </summary>
    public string? BuildSourceVersion { get; set; }

    /// <summary>
    /// Local repository path for accessing files directly from the filesystem instead of Azure DevOps API.
    /// When running in Azure Pipelines, this should be set to $(Build.SourcesDirectory).
    /// This dramatically reduces API calls and avoids rate limiting for large codebases.
    /// Loaded from LOCAL_REPO_PATH environment variable.
    /// </summary>
    [Required]
    public string LocalRepoPath { get; set; } = string.Empty;

    /// <summary>
    /// Azure Service Bus connection string for receiving pull request events.
    /// Loaded from AZURE_SERVICEBUS_CONNECTION_STRING environment variable.
    /// </summary>
    [Required]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Source control provider to use (AzureDevOps, GitHub, GitLab, etc.).
    /// Loaded from SOURCE_PROVIDER environment variable or Provider:SourceProvider in settings.ini.
    /// Defaults to AzureDevOps for backward compatibility.
    /// </summary>
    public SourceProvider SourceProvider { get; set; } = SourceProvider.AzureDevOps;

    // ========================================================================
    // Static values - loaded from settings.ini via automatic binding
    // ========================================================================

    /// <summary>
    /// AI model configuration settings.
    /// Maps to [AI] section in settings.ini.
    /// </summary>
    [Required]
    public AiOptions AI { get; set; } = new();

    /// <summary>
    /// Function calling configuration settings.
    /// Maps to [FunctionCalling] section in settings.ini.
    /// </summary>
    [Required]
    public FunctionCallingOptions FunctionCalling { get; set; } = new();

    /// <summary>
    /// Review behavior settings.
    /// Maps to [Review] section in settings.ini.
    /// </summary>
    [Required]
    public ReviewBehaviorOptions Review { get; set; } = new();

    /// <summary>
    /// File processing settings.
    /// Maps to [Files] section in settings.ini.
    /// </summary>
    [Required]
    public FilesOptions Files { get; set; } = new();

    /// <summary>
    /// Language detection settings.
    /// Maps to [Language] section in settings.ini.
    /// </summary>
    [Required]
    public LanguageOptions Language { get; set; } = new();

    /// <summary>
    /// Queue processing settings.
    /// Maps to [Queue] section in settings.ini.
    /// </summary>
    [Required]
    public QueueOptions Queue { get; set; } = new();

    // ========================================================================
    // Backward compatibility properties (for existing code)
    // ========================================================================

    /// <summary>
    /// Azure OpenAI deployment/model name to use for reviews.
    /// Shortcut to AI.Deployment for backward compatibility.
    /// </summary>
    public string AiFoundryDeployment
    {
        get => AI.Deployment;
        set => AI.Deployment = value;
    }

    /// <summary>
    /// Temperature parameter for AI model (0.0-1.0). Lower values are more deterministic.
    /// Shortcut to AI.Temperature for backward compatibility.
    /// </summary>
    public double AiTemperature
    {
        get => AI.Temperature;
        set => AI.Temperature = value;
    }

    /// <summary>
    /// Maximum number of tokens for AI response.
    /// Shortcut to AI.MaxTokens for backward compatibility.
    /// </summary>
    public int AiMaxTokens
    {
        get => AI.MaxTokens;
        set => AI.MaxTokens = value;
    }

    /// <summary>
    /// Enables OpenAI function calling.
    /// Shortcut to FunctionCalling.Enabled for backward compatibility.
    /// </summary>
    public bool EnableFunctionCalling
    {
        get => FunctionCalling.Enabled;
        set => FunctionCalling.Enabled = value;
    }

    /// <summary>
    /// Maximum number of function calls the AI can make during a single review.
    /// Shortcut to FunctionCalling.MaxCalls for backward compatibility.
    /// </summary>
    public int MaxFunctionCalls
    {
        get => FunctionCalling.MaxCalls;
        set => FunctionCalling.MaxCalls = value;
    }

    /// <summary>
    /// When true, performs review without posting comments or approvals to Azure DevOps.
    /// Shortcut to Review.DryRun for backward compatibility.
    /// </summary>
    public bool DryRun
    {
        get => Review.DryRun;
        set => Review.DryRun = value;
    }

    /// <summary>
    /// When true, only run the review if the PAT user has been added as a required reviewer to the PR.
    /// Shortcut to Review.OnlyReviewIfRequiredReviewer for backward compatibility.
    /// </summary>
    public bool OnlyReviewIfRequiredReviewer
    {
        get => Review.OnlyReviewIfRequiredReviewer;
        set => Review.OnlyReviewIfRequiredReviewer = value;
    }

    /// <summary>
    /// Scope of files to review.
    /// Shortcut to Review.Scope for backward compatibility.
    /// </summary>
    public string ReviewScope
    {
        get => Review.Scope;
        set => Review.Scope = value;
    }

    /// <summary>
    /// Maximum number of warnings before rejecting approval.
    /// Shortcut to Review.WarnBudget for backward compatibility.
    /// </summary>
    public int WarnBudget
    {
        get => Review.WarnBudget;
        set => Review.WarnBudget = value;
    }

    /// <summary>
    /// Path to the review policy markdown file.
    /// Shortcut to Review.PolicyPath for backward compatibility.
    /// </summary>
    public string PolicyPath
    {
        get => Review.PolicyPath;
        set => Review.PolicyPath = value;
    }

    /// <summary>
    /// Base path to the prompts directory.
    /// Shortcut to Review.PromptsBasePath for backward compatibility.
    /// </summary>
    public string PromptsBasePath
    {
        get => Review.PromptsBasePath;
        set => Review.PromptsBasePath = value;
    }

    /// <summary>
    /// Maximum file size in bytes to review.
    /// Shortcut to Files.MaxFileBytes for backward compatibility.
    /// </summary>
    public int MaxFileBytes
    {
        get => Files.MaxFileBytes;
        set => Files.MaxFileBytes = value;
    }

    /// <summary>
    /// Maximum diff size in bytes to send to AI.
    /// Shortcut to Files.MaxDiffBytes for backward compatibility.
    /// </summary>
    public int MaxDiffBytes
    {
        get => Files.MaxDiffBytes;
        set => Files.MaxDiffBytes = value;
    }

    /// <summary>
    /// Maximum number of files to review in a single PR.
    /// Shortcut to Files.MaxFilesToReview for backward compatibility.
    /// </summary>
    public int MaxFilesToReview
    {
        get => Files.MaxFilesToReview;
        set => Files.MaxFilesToReview = value;
    }

    /// <summary>
    /// Maximum number of issues to report per file.
    /// Shortcut to Files.MaxIssuesPerFile for backward compatibility.
    /// </summary>
    public int MaxIssuesPerFile
    {
        get => Files.MaxIssuesPerFile;
        set => Files.MaxIssuesPerFile = value;
    }

    /// <summary>
    /// Maximum number of commit messages to include in metadata review.
    /// Shortcut to Files.MaxCommitMessagesToReview for backward compatibility.
    /// </summary>
    public int MaxCommitMessagesToReview
    {
        get => Files.MaxCommitMessagesToReview;
        set => Files.MaxCommitMessagesToReview = value;
    }

    /// <summary>
    /// Maximum diff size in bytes to include in AI prompt.
    /// Shortcut to Files.MaxPromptDiffBytes for backward compatibility.
    /// </summary>
    public int MaxPromptDiffBytes
    {
        get => Files.MaxPromptDiffBytes;
        set => Files.MaxPromptDiffBytes = value;
    }

    /// <summary>
    /// Threshold ratio for detecting Japanese language in PR descriptions.
    /// Shortcut to Language.JapaneseDetectionThreshold for backward compatibility.
    /// </summary>
    public double JapaneseDetectionThreshold
    {
        get => Language.JapaneseDetectionThreshold;
        set => Language.JapaneseDetectionThreshold = value;
    }

    /// <summary>
    /// Normalizes option values (e.g., path separators).
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

/// <summary>
/// AI model configuration settings.
/// Maps to [AI] section in settings.ini.
/// </summary>
public class AiOptions
{
    /// <summary>
    /// The name of your Azure OpenAI deployment.
    /// Maps to AI:Deployment in settings.ini.
    /// </summary>
    [Required]
    public string Deployment { get; set; } = string.Empty;

    /// <summary>
    /// Temperature parameter for AI model (0.0-1.0). Lower values are more deterministic.
    /// Maps to AI:Temperature in settings.ini.
    /// </summary>
    [Required]
    public double Temperature { get; set; }

    /// <summary>
    /// Maximum number of tokens for AI response.
    /// Maps to AI:MaxTokens in settings.ini.
    /// </summary>
    [Required]
    public int MaxTokens { get; set; }
}

/// <summary>
/// Function calling configuration settings.
/// Maps to [FunctionCalling] section in settings.ini.
/// </summary>
public class FunctionCallingOptions
{
    /// <summary>
    /// Enable OpenAI function calling feature.
    /// Maps to FunctionCalling:Enabled in settings.ini.
    /// </summary>
    [Required]
    public bool Enabled { get; set; }

    /// <summary>
    /// Maximum number of function calls the AI can make per file review.
    /// Maps to FunctionCalling:MaxCalls in settings.ini.
    /// </summary>
    [Required]
    public int MaxCalls { get; set; }

    /// <summary>
    /// Maximum number of conversation iterations in a single AI interaction.
    /// Prevents infinite loops in function calling scenarios.
    /// Maps to FunctionCalling:MaxConversationIterations in settings.ini.
    /// </summary>
    [Required]
    public int MaxConversationIterations { get; set; }

    /// <summary>
    /// Maximum size in bytes for diff content returned by get_pr_diff function.
    /// Large diffs are truncated to this size to avoid token limits.
    /// Maps to FunctionCalling:MaxDiffSizeBytes in settings.ini.
    /// </summary>
    [Required]
    public int MaxDiffSizeBytes { get; set; }
}

/// <summary>
/// Review behavior settings.
/// Maps to [Review] section in settings.ini.
/// </summary>
public class ReviewBehaviorOptions
{
    /// <summary>
    /// Test mode - performs review without posting to Azure DevOps.
    /// Maps to Review:DryRun in settings.ini.
    /// </summary>
    [Required]
    public bool DryRun { get; set; }

    /// <summary>
    /// Only run the review if the PAT user has been added as a required reviewer.
    /// Maps to Review:OnlyReviewIfRequiredReviewer in settings.ini.
    /// </summary>
    [Required]
    public bool OnlyReviewIfRequiredReviewer { get; set; }

    /// <summary>
    /// Scope of files to review.
    /// Maps to Review:Scope in settings.ini.
    /// </summary>
    [Required]
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of warnings before rejecting approval.
    /// Maps to Review:WarnBudget in settings.ini.
    /// </summary>
    [Required]
    public int WarnBudget { get; set; }

    /// <summary>
    /// Path to the review policy markdown file.
    /// Maps to Review:PolicyPath in settings.ini.
    /// </summary>
    [Required]
    public string PolicyPath { get; set; } = string.Empty;

    /// <summary>
    /// Base path to the prompts directory.
    /// Maps to Review:PromptsBasePath in settings.ini.
    /// </summary>
    [Required]
    public string PromptsBasePath { get; set; } = string.Empty;
}

/// <summary>
/// File processing settings.
/// Maps to [Files] section in settings.ini.
/// </summary>
public class FilesOptions
{
    /// <summary>
    /// Maximum number of files to review in a single PR.
    /// Maps to Files:MaxFilesToReview in settings.ini.
    /// </summary>
    [Required]
    public int MaxFilesToReview { get; set; }

    /// <summary>
    /// Maximum number of issues to report per file.
    /// Maps to Files:MaxIssuesPerFile in settings.ini.
    /// </summary>
    [Required]
    public int MaxIssuesPerFile { get; set; }

    /// <summary>
    /// Maximum file size to review. Supports human-readable formats (e.g., "200KB", "1.5MB").
    /// Maps to Files:MaxFileBytes in settings.ini.
    /// Automatically parsed from string using SizeTypeConverter.
    /// </summary>
    [Required]
    public Size MaxFileBytes { get; set; }

    /// <summary>
    /// Maximum diff size to send to AI. Supports human-readable formats (e.g., "500KB", "1MB").
    /// Maps to Files:MaxDiffBytes in settings.ini.
    /// Automatically parsed from string using SizeTypeConverter.
    /// </summary>
    [Required]
    public Size MaxDiffBytes { get; set; }

    /// <summary>
    /// Maximum diff size to include in AI prompt. Supports human-readable formats (e.g., "8KB", "16KB").
    /// Maps to Files:MaxPromptDiffBytes in settings.ini.
    /// Automatically parsed from string using SizeTypeConverter.
    /// </summary>
    [Required]
    public Size MaxPromptDiffBytes { get; set; }

    /// <summary>
    /// Maximum number of commit messages to include in metadata review.
    /// Maps to Files:MaxCommitMessagesToReview in settings.ini.
    /// </summary>
    [Required]
    public int MaxCommitMessagesToReview { get; set; }
}

/// <summary>
/// Language detection settings.
/// Maps to [Language] section in settings.ini.
/// </summary>
public class LanguageOptions
{
    /// <summary>
    /// Threshold ratio (0.0 to 1.0) for detecting Japanese language in PR descriptions.
    /// Maps to Language:JapaneseDetectionThreshold in settings.ini.
    /// </summary>
    [Required]
    public double JapaneseDetectionThreshold { get; set; }
}

/// <summary>
/// Queue processing settings.
/// Maps to [Queue] section in settings.ini.
/// </summary>
public class QueueOptions
{
    /// <summary>
    /// The name of the Azure Service Bus queue to receive PR events from.
    /// Maps to Queue:QueueName in settings.ini.
    /// </summary>
    [Required]
    public string QueueName { get; set; } = string.Empty;

    /// <summary>
    /// The display name of the AI bot for @mention detection.
    /// Maps to Queue:BotDisplayName in settings.ini.
    /// </summary>
    [Required]
    public string BotDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of concurrent messages to process.
    /// Maps to Queue:MaxConcurrentCalls in settings.ini.
    /// </summary>
    [Required]
    public int MaxConcurrentCalls { get; set; }

    /// <summary>
    /// Maximum time to wait for a message before checking cancellation.
    /// Maps to Queue:MaxWaitTimeSeconds in settings.ini.
    /// </summary>
    [Required]
    public int MaxWaitTimeSeconds { get; set; }

    /// <summary>
    /// Maximum number of delivery attempts before moving message to dead-letter queue.
    /// Maps to Queue:MaxDeliveryAttempts in settings.ini.
    /// </summary>
    [Required]
    public int MaxDeliveryAttempts { get; set; }

    /// <summary>
    /// Duration in minutes to automatically renew message lock during processing.
    /// Maps to Queue:MessageLockRenewalMinutes in settings.ini.
    /// </summary>
    [Required]
    public int MessageLockRenewalMinutes { get; set; }
}
