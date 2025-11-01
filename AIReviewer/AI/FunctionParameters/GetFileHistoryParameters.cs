using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AIReviewer.AI.FunctionParameters;

/// <summary>
/// Parameters for getting the commit history of a file.
/// </summary>
public class GetFileHistoryParameters
{
    /// <summary>
    /// The path to the file
    /// </summary>
    [Required]
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of commits to return (default: 5, max: 10)
    /// </summary>
    [JsonPropertyName("maxCommits")]
    public int? MaxCommits { get; set; }
}
