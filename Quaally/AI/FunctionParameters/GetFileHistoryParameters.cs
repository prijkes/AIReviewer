using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Quaally.AI.FunctionParameters;

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
    /// Maximum number of commits to return (default: 5, max: 30)
    /// </summary>
    [JsonPropertyName("maxCommits")]
    [Range(1, FunctionDefaults.FileHistoryMaxCommitsLimit)]
    public int? MaxCommits { get; set; }
}
