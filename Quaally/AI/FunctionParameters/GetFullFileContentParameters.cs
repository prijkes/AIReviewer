using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Quaally.AI.FunctionParameters;

/// <summary>
/// Parameters for getting full file content from the target branch.
/// </summary>
public class GetFullFileContentParameters
{
    /// <summary>
    /// The path to the file (e.g., 'src/Program.cs', 'AIReviewer/Review/ReviewPlanner.cs')
    /// </summary>
    [Required]
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;
}
