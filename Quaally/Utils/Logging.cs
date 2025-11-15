namespace Quaally.Utils;

/// <summary>
/// Utility class for logging-related operations including cryptographic hashing.
/// </summary>
public static class Logging
{
    /// <summary>
    /// Computes a SHA-256 hash of the input string and returns it as a hexadecimal string.
    /// Used for generating fingerprints to track issues across PR iterations.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <returns>A hexadecimal string representation of the SHA-256 hash.</returns>
    public static string HashSha256(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    /// <summary>
    /// Detects whether text is primarily in Japanese or English based on character analysis.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <returns>"ja" for Japanese, "en" for English.</returns>
    public static string DetectLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "en"; // Default to English
        }

        var japaneseChars = 0;
        var totalChars = 0;

        foreach (var ch in text)
        {
            // Skip whitespace and punctuation
            if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch))
            {
                continue;
            }

            totalChars++;

            // Check for Japanese character ranges:
            // Hiragana: U+3040 to U+309F
            // Katakana: U+30A0 to U+30FF
            // CJK Unified Ideographs (Kanji): U+4E00 to U+9FAF
            // Fullwidth forms: U+FF00 to U+FFEF
            if ((ch >= '\u3040' && ch <= '\u309F') ||  // Hiragana
                (ch >= '\u30A0' && ch <= '\u30FF') ||  // Katakana
                (ch >= '\u4E00' && ch <= '\u9FAF') ||  // Kanji
                (ch >= '\uFF00' && ch <= '\uFFEF'))    // Fullwidth
            {
                japaneseChars++;
            }
        }

        // If more than 30% of characters are Japanese, consider it Japanese text
        if (totalChars > 0 && (double)japaneseChars / totalChars > 0.3)
        {
            return "ja";
        }

        return "en";
    }
}
