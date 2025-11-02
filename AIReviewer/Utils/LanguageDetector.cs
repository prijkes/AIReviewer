using System.Text.RegularExpressions;

namespace AIReviewer.Utils;

/// <summary>
/// Utility for detecting the primary language of text content.
/// Currently supports English and Japanese detection.
/// </summary>
public static partial class LanguageDetector
{
    /// <summary>
    /// Detects the primary language of the given text.
    /// Returns "ja" for Japanese if more than 30% of non-whitespace characters are Japanese,
    /// otherwise returns "en" for English.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <returns>Language code: "ja" for Japanese, "en" for English.</returns>
    public static string DetectLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "en";
        }

        var japaneseChars = JapaneseCharRegex().Matches(text).Count;
        var nonWhitespace = text.Count(c => !char.IsWhiteSpace(c));

        if (nonWhitespace == 0)
        {
            return "en";
        }

        var japaneseRatio = (double)japaneseChars / nonWhitespace;
        return japaneseRatio > 0.3 ? "ja" : "en";
    }

    /// <summary>
    /// Regular expression for matching Japanese characters (Hiragana, Katakana, Kanji, Fullwidth).
    /// </summary>
    [GeneratedRegex(@"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}\uFF00-\uFFEF]")]
    private static partial Regex JapaneseCharRegex();
}
