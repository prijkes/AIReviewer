using AIReviewer.Options;
using AIReviewer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIReviewer.AI;

/// <summary>
/// Service for loading prompt template files.
/// Prompt templates are kept in markdown files for easy editing without recompilation.
/// Supports system, instruction, and language-specific prompts.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PromptLoader"/> class.
/// </remarks>
/// <param name="logger">Logger for diagnostic information.</param>
/// <param name="options">Configuration options for the reviewer.</param>
public sealed class PromptLoader(ILogger<PromptLoader> logger, IOptionsMonitor<ReviewerOptions> options)
{
    private readonly ReviewerOptions _options = options.CurrentValue;
    private readonly Dictionary<string, string> _promptCache = [];

    /// <summary>
    /// Loads the system prompt for a specific programming language.
    /// </summary>
    /// <param name="programmingLanguage">The programming language.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The system prompt content.</returns>
    public async Task<string> LoadSystemPromptAsync(
        ProgrammingLanguageDetector.ProgrammingLanguage programmingLanguage,
        CancellationToken cancellationToken)
    {
        var fileName = GetSystemPromptFileName(programmingLanguage);
        var path = Path.Combine(_options.PromptsBasePath, "system", fileName);
        return await LoadPromptAsync(path, cancellationToken);
    }

    /// <summary>
    /// Loads the file review instruction prompt.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The file review instruction content.</returns>
    public async Task<string> LoadFileReviewInstructionAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(_options.PromptsBasePath, "instructions", "file-review.md");
        return await LoadPromptAsync(path, cancellationToken);
    }

    /// <summary>
    /// Loads the metadata review instruction prompt.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The metadata review instruction content.</returns>
    public async Task<string> LoadMetadataReviewInstructionAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(_options.PromptsBasePath, "instructions", "metadata-review.md");
        return await LoadPromptAsync(path, cancellationToken);
    }

    /// <summary>
    /// Loads the language instruction prompt for review responses.
    /// </summary>
    /// <param name="language">Language code (e.g., "en" or "ja").</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The language instruction content.</returns>
    public async Task<string> LoadLanguageInstructionAsync(string language, CancellationToken cancellationToken)
    {
        var fileName = language == "ja" ? "japanese.md" : "english.md";
        var path = Path.Combine(_options.PromptsBasePath, "language", fileName);
        return await LoadPromptAsync(path, cancellationToken);
    }

    /// <summary>
    /// Loads a prompt file from disk with caching.
    /// </summary>
    /// <param name="path">The relative path to the prompt file.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The prompt content as text.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the prompt file doesn't exist.</exception>
    private async Task<string> LoadPromptAsync(string path, CancellationToken cancellationToken)
    {
        // Return cached prompt if available
        if (_promptCache.TryGetValue(path, out var cachedPrompt))
        {
            return cachedPrompt;
        }

        _options.Normalize();
        var fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Prompt file not found at {fullPath}");
        }

        var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
        logger.LogDebug("Loaded prompt file {PromptPath} (chars: {Length})", fullPath, content.Length);
        
        _promptCache[path] = content;
        return content;
    }

    /// <summary>
    /// Gets the system prompt file name for a specific programming language.
    /// </summary>
    private static string GetSystemPromptFileName(ProgrammingLanguageDetector.ProgrammingLanguage language)
    {
        return language switch
        {
            ProgrammingLanguageDetector.ProgrammingLanguage.CSharp => "base-csharp.md",
            ProgrammingLanguageDetector.ProgrammingLanguage.Cpp => "base-cpp.md",
            ProgrammingLanguageDetector.ProgrammingLanguage.C => "base-c.md",
            ProgrammingLanguageDetector.ProgrammingLanguage.Cli => "base-cli.md",
            _ => "base-unknown.md"
        };
    }
}
