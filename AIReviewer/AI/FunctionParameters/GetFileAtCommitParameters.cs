using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AIReviewer.AI.FunctionParameters;

/// <summary>
/// Parameters for getting file content at a specific commit or branch.
/// </summary>
public class GetFileAtCommitParameters
{
    /// <summary>
    /// The path to the file
    /// </summary>
    [Required]
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// The commit SHA (40 characters) or branch name (e.g., 'main', 'develop')
    /// </summary>
    [Required]
    [JsonPropertyName("commitOrBranch")]
    public string CommitOrBranch { get; set; } = string.Empty;
}
