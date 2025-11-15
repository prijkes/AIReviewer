using System.ComponentModel;

namespace Quaaly.Infrastructure.AzureDevOps.Functions.Parameters;

/// <summary>
/// Parameters for adding a reviewer to a pull request.
/// </summary>
public class AddReviewerParameters
{
    /// <summary>
    /// The unique identifier (email or ID) of the reviewer to add.
    /// </summary>
    [Description("The unique identifier (email or ID) of the reviewer to add")]
    public string ReviewerId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the reviewer is required.
    /// </summary>
    [Description("Whether the reviewer is required")]
    public bool IsRequired { get; set; }
}

/// <summary>
/// Parameters for updating a pull request's description.
/// </summary>
public class UpdatePullRequestDescriptionParameters
{
    /// <summary>
    /// The new description text.
    /// </summary>
    [Description("The new description text")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Parameters for adding a label to a pull request.
/// </summary>
public class AddPullRequestLabelParameters
{
    /// <summary>
    /// The label/tag to add.
    /// </summary>
    [Description("The label/tag to add")]
    public string Label { get; set; } = string.Empty;
}
