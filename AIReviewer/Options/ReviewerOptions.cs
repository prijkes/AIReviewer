using System.ComponentModel.DataAnnotations;

namespace AIReviewer.Reviewer.Options;

public sealed class ReviewerOptions
{
    [Required]
    public string AdoCollectionUrl { get; set; } = string.Empty;

    [Required]
    public string AdoProject { get; set; } = string.Empty;

    public string? AdoRepoId { get; set; }

    public string? AdoRepoName { get; set; }

    public int? AdoPullRequestId { get; set; }

    [Required]
    public string AdoAccessToken { get; set; } = string.Empty;

    [Required]
    public string AiFoundryEndpoint { get; set; } = string.Empty;

    public string AiFoundryDeployment { get; set; } = "o4-mini";

    [Required]
    public string AiFoundryApiKey { get; set; } = string.Empty;

    public double AiTemperature { get; set; } = 0.2;

    public int AiMaxTokens { get; set; } = 2000;

    public bool DryRun { get; set; } = false;

    public string ReviewScope { get; set; } = "changed-files";

    public int MaxFileBytes { get; set; } = 200000;

    public int MaxDiffBytes { get; set; } = 500000;

    public int WarnBudget { get; set; } = 3;

    public string PolicyPath { get; set; } = "./policy/review-policy.md";

    public string? BuildSourceVersion { get; set; }

    public void Normalize()
    {
        ReviewScope = string.IsNullOrWhiteSpace(ReviewScope) ? "changed-files" : ReviewScope;
        AiFoundryDeployment = string.IsNullOrWhiteSpace(AiFoundryDeployment) ? "o4-mini" : AiFoundryDeployment;
        PolicyPath = string.IsNullOrWhiteSpace(PolicyPath) ? "./policy/review-policy.md" : PolicyPath;
    }
}
