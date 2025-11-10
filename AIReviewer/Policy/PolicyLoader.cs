using AIReviewer.Options;
using AIReviewer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIReviewer.Policy;

/// <summary>
/// Service for loading review policy files.
/// Policy files are kept in their original markdown format for optimal AI comprehension.
/// Supports language-specific policy files with fallback to general policy.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PolicyLoader"/> class.
/// </remarks>
/// <param name="logger">Logger for diagnostic information.</param>
/// <param name="options">Configuration options for the reviewer.</param>
public sealed class PolicyLoader(ILogger<PolicyLoader> logger, IOptionsMonitor<ReviewerOptions> options)
{
    private readonly ReviewerOptions _options = options.CurrentValue;
    private readonly Dictionary<string, string> _policyCache = [];

    /// <summary>
    /// Loads a policy file from disk in its original markdown format.
    /// The policy is cached to avoid repeated file I/O.
    /// </summary>
    /// <param name="path">The relative path to the policy file.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The policy content as markdown text.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the policy file doesn't exist.</exception>
    public async Task<string> LoadAsync(string path, CancellationToken cancellationToken)
    {
        // Return cached policy if available
        if (_policyCache.TryGetValue(path, out var cachedPolicy))
        {
            return cachedPolicy;
        }

        var fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Policy file not found at {fullPath}");
        }

        var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
        logger.LogInformation("Loaded policy file {PolicyPath} (chars: {Length})", fullPath, content.Length);

        _policyCache[path] = content;
        return content;
    }

    /// <summary>
    /// Loads a language-specific policy file if available, otherwise falls back to the general policy.
    /// The policy is cached to avoid repeated file I/O.
    /// </summary>
    /// <param name="basePolicyPath">The base policy file path (e.g., "./policy/review-policy.md").</param>
    /// <param name="programmingLanguage">The programming language to load policy for.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The policy content as markdown text.</returns>
    public async Task<string> LoadLanguageSpecificAsync(
        string basePolicyPath,
        ProgrammingLanguageDetector.ProgrammingLanguage programmingLanguage,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{basePolicyPath}:{programmingLanguage}";

        // Return cached policy if available
        if (_policyCache.TryGetValue(cacheKey, out var cachedPolicy))
        {
            return cachedPolicy;
        }

        // Try to load language-specific policy file first
        var languageSpecificPath = GetLanguageSpecificPolicyPath(basePolicyPath, programmingLanguage);
        var fullLanguageSpecificPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), languageSpecificPath));

        if (File.Exists(fullLanguageSpecificPath))
        {
            var content = await File.ReadAllTextAsync(fullLanguageSpecificPath, cancellationToken);
            logger.LogInformation("Loaded language-specific policy file {PolicyPath} for {Language} (chars: {Length})",
                fullLanguageSpecificPath,
                ProgrammingLanguageDetector.GetDisplayName(programmingLanguage),
                content.Length);

            _policyCache[cacheKey] = content;
            return content;
        }

        // Fall back to general policy
        logger.LogDebug("Language-specific policy not found at {Path}, falling back to general policy", fullLanguageSpecificPath);
        var generalPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), basePolicyPath));

        if (!File.Exists(generalPath))
        {
            throw new FileNotFoundException($"Policy file not found at {generalPath}");
        }

        var generalContent = await File.ReadAllTextAsync(generalPath, cancellationToken);
        logger.LogInformation("Loaded general policy file {PolicyPath} (chars: {Length})", generalPath, generalContent.Length);

        _policyCache[cacheKey] = generalContent;
        return generalContent;
    }

    /// <summary>
    /// Generates a language-specific policy file path based on the base path.
    /// For example: "./policy/review-policy.md" -> "./policy/review-policy-csharp.md"
    /// </summary>
    private static string GetLanguageSpecificPolicyPath(string basePath, ProgrammingLanguageDetector.ProgrammingLanguage language)
    {
        var directory = Path.GetDirectoryName(basePath) ?? "./policy";
        var fileName = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);
        var languageSuffix = ProgrammingLanguageDetector.GetPolicySuffix(language);

        return Path.Combine(directory, $"{fileName}-{languageSuffix}{extension}");
    }
}
