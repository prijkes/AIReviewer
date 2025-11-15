namespace Quaally.Utils;

/// <summary>
/// Detects the programming language of a file based on its extension.
/// Used to apply language-specific review prompts and policies.
/// </summary>
public static class ProgrammingLanguageDetector
{
    /// <summary>
    /// Supported programming languages for code review.
    /// </summary>
    public enum ProgrammingLanguage
    {
        /// <summary>C# programming language</summary>
        CSharp,
        /// <summary>C++ programming language</summary>
        Cpp,
        /// <summary>C programming language</summary>
        C,
        /// <summary>CLI/C++/CLI programming language</summary>
        Cli,
        /// <summary>Unknown or unsupported language</summary>
        Unknown
    }

    /// <summary>
    /// Detects the programming language based on file extension.
    /// </summary>
    /// <param name="filePath">The file path to analyze.</param>
    /// <returns>The detected programming language.</returns>
    public static ProgrammingLanguage DetectLanguage(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ProgrammingLanguage.Unknown;
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".cs" => ProgrammingLanguage.CSharp,
            ".cpp" or ".cxx" or ".cc" => ProgrammingLanguage.Cpp,
            ".h" or ".hpp" or ".hxx" => DetermineHeaderLanguage(filePath),
            ".c" => ProgrammingLanguage.C,
            ".cli" => ProgrammingLanguage.Cli,
            _ => ProgrammingLanguage.Unknown
        };
    }

    /// <summary>
    /// Determines whether a header file is C or C++ based on context.
    /// Defaults to C++ for header files as it's more common in mixed codebases.
    /// </summary>
    private static ProgrammingLanguage DetermineHeaderLanguage(string filePath)
    {
        // .hpp and .hxx are almost always C++
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension is ".hpp" or ".hxx")
        {
            return ProgrammingLanguage.Cpp;
        }

        // .h could be either C or C++
        // Default to C++ as it's more common in modern codebases
        // A more sophisticated approach could check file contents for C++ keywords
        return ProgrammingLanguage.Cpp;
    }

    /// <summary>
    /// Gets a human-readable display name for a programming language.
    /// </summary>
    /// <param name="language">The programming language.</param>
    /// <returns>Display name for the language.</returns>
    public static string GetDisplayName(ProgrammingLanguage language)
    {
        return language switch
        {
            ProgrammingLanguage.CSharp => "C#/.NET",
            ProgrammingLanguage.Cpp => "C++",
            ProgrammingLanguage.C => "C",
            ProgrammingLanguage.Cli => "C++/CLI",
            ProgrammingLanguage.Unknown => "Unknown",
            _ => language.ToString()
        };
    }

    /// <summary>
    /// Gets the policy file suffix for a programming language.
    /// Used to load language-specific policy files (e.g., "csharp", "cpp").
    /// </summary>
    /// <param name="language">The programming language.</param>
    /// <returns>Policy file suffix for the language.</returns>
    public static string GetPolicySuffix(ProgrammingLanguage language)
    {
        return language switch
        {
            ProgrammingLanguage.CSharp => "csharp",
            ProgrammingLanguage.Cpp => "cpp",
            ProgrammingLanguage.C => "c",
            ProgrammingLanguage.Cli => "cli",
            ProgrammingLanguage.Unknown => "general",
            _ => "general"
        };
    }
}
