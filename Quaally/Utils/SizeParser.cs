namespace Quaally.Utils;

/// <summary>
/// Parses human-readable size strings (e.g., "200KB", "1.5GB") to bytes.
/// </summary>
public static class SizeParser
{
    /// <summary>
    /// Parses a size string to bytes.
    /// </summary>
    /// <param name="sizeString">Size string like "200KB", "1.5GB", "500MB", or "1000" (plain number = bytes)</param>
    /// <returns>Size in bytes</returns>
    /// <exception cref="FormatException">Thrown when size string is invalid</exception>
    public static int ParseToBytes(string sizeString)
    {
        if (string.IsNullOrWhiteSpace(sizeString))
        {
            throw new FormatException("Size string cannot be empty");
        }

        sizeString = sizeString.Trim().ToUpperInvariant();

        // If it's just a number, treat as bytes
        if (double.TryParse(sizeString, out var bytes))
        {
            return (int)bytes;
        }

        // Extract numeric part and unit
        var numericPart = new string(sizeString.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        var unitPart = sizeString.Substring(numericPart.Length).Trim();

        if (!double.TryParse(numericPart, out var value))
        {
            throw new FormatException($"Invalid size format: '{sizeString}'. Expected format: '200KB', '1.5GB', '500MB', or plain number.");
        }

        var multiplier = unitPart switch
        {
            "B" or "" => 1L,
            "KB" => 1024L,
            "MB" => 1024L * 1024L,
            "GB" => 1024L * 1024L * 1024L,
            _ => throw new FormatException($"Unknown size unit: '{unitPart}'. Valid units: B, KB, MB, GB")
        };

        var result = (long)(value * multiplier);

        if (result > int.MaxValue)
        {
            throw new FormatException($"Size too large: '{sizeString}' = {result} bytes (max: {int.MaxValue} bytes)");
        }

        return (int)result;
    }

    /// <summary>
    /// Formats bytes to a human-readable string.
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return bytes switch
        {
            >= GB => $"{bytes / (double)GB:0.##}GB",
            >= MB => $"{bytes / (double)MB:0.##}MB",
            >= KB => $"{bytes / (double)KB:0.##}KB",
            _ => $"{bytes}B"
        };
    }
}
