using AIReviewer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIReviewer.Policy;

/// <summary>
/// Service for loading review policy files.
/// Policy files are kept in their original markdown format for optimal AI comprehension.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PolicyLoader"/> class.
/// </remarks>
/// <param name="logger">Logger for diagnostic information.</param>
/// <param name="options">Configuration options for the reviewer.</param>
public sealed class PolicyLoader(ILogger<PolicyLoader> logger, IOptionsMonitor<ReviewerOptions> options)
{
    private readonly ReviewerOptions _options = options.CurrentValue;

    /// <summary>
    /// Loads a policy file from disk in its original markdown format.
    /// </summary>
    /// <param name="path">The relative path to the policy file.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The policy content as markdown text.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the policy file doesn't exist.</exception>
    public async Task<string> LoadAsync(string path, CancellationToken cancellationToken)
    {
        _options.Normalize();
        var fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Policy file not found at {fullPath}");
        }

        var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
        logger.LogInformation("Loaded policy file {PolicyPath} (chars: {Length})", fullPath, content.Length);
        return content;
    }
}
