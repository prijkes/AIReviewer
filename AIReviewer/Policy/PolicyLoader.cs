using AIReviewer.Reviewer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIReviewer.Reviewer.Policy;

public sealed class PolicyLoader
{
    private readonly ILogger<PolicyLoader> _logger;
    private readonly ReviewerOptions _options;

    public PolicyLoader(ILogger<PolicyLoader> logger, IOptionsMonitor<ReviewerOptions> options)
    {
        _logger = logger;
        _options = options.CurrentValue;
    }

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
