using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Quaaly.Infrastructure.AI.FunctionParameters;

/// <summary>
/// Parameters for searching the codebase for text or patterns.
/// </summary>
public class SearchCodebaseParameters
{
    /// <summary>
    /// The text to search for (e.g., 'class MyClass', 'void ProcessData', 'interface IService')
    /// </summary>
    [Required]
    [JsonPropertyName("searchTerm")]
    public string SearchTerm { get; set; } = string.Empty;

    /// <summary>
    /// Optional file pattern filter (e.g., '*.cs', '*.csproj', '*.json'). Leave empty to search all files.
    /// </summary>
    [JsonPropertyName("filePattern")]
    public string? FilePattern { get; set; }

    /// <summary>
    /// Maximum number of results to return (default: 10, max: 100)
    /// </summary>
    [JsonPropertyName("maxResults")]
    [Range(1, FunctionDefaults.SearchMaxResultsLimit)]
    public int? MaxResults { get; set; }
}
