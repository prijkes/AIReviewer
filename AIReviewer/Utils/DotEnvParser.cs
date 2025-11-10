namespace AIReviewer.Utils;

/// <summary>
/// Parses POSIX-style .env files and loads environment variables.
/// </summary>
public static class DotEnvParser
{
    /// <summary>
    /// Parses a .env file and returns the key-value pairs.
    /// Also sets the values as environment variables.
    /// </summary>
    /// <param name="envFile">Path to the .env file.</param>
    /// <returns>Dictionary of environment variable key-value pairs.</returns>
    public static Dictionary<string, string?> Parse(string envFile)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(envFile))
        {
            return data;
        }

        foreach (var rawLine in File.ReadAllLines(envFile))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var line = rawLine.Trim();

            // Skip comments
            if (line.StartsWith('#'))
            {
                continue;
            }

            // Handle 'export' prefix
            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
            {
                line = line["export ".Length..].TrimStart();
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            // Remove quotes if present
            if (value.Length >= 2 &&
                ((value.StartsWith('"') && value.EndsWith('"')) ||
                 (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            data[key] = value;
            Environment.SetEnvironmentVariable(key, value);
        }

        return data;
    }
}
