using AIReviewer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIReviewer.Policy;

/// <summary>
/// Service for loading and processing review policy files.
/// Converts markdown policy files to plain text for AI consumption.
/// </summary>
public sealed class PolicyLoader
{
    private readonly ILogger<PolicyLoader> _logger;
    private readonly ReviewerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyLoader"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="options">Configuration options for the reviewer.</param>
    public PolicyLoader(ILogger<PolicyLoader> logger, IOptionsMonitor<ReviewerOptions> options)
    {
        _logger = logger;
        _options = options.CurrentValue;
    }

    /// <summary>
    /// Loads a policy file from disk and converts it from markdown to plain text.
    /// </summary>
    /// <param name="path">The relative path to the policy file.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The policy content as plain text.</returns>
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
        var sanitized = Markdig.Markdown.ToPlainText(content);
        _logger.LogInformation("Loaded policy file {PolicyPath} (chars: {Length})", fullPath, sanitized.Length);
        return sanitized;
    }
}
