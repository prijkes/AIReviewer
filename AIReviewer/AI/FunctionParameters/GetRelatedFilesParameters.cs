using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AIReviewer.AI.FunctionParameters;

/// <summary>
/// Parameters for finding files related to a specified file.
/// </summary>
public class GetRelatedFilesParameters
{
    /// <summary>
    /// The file to find related files for
    /// </summary>
    [Required]
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;
}
