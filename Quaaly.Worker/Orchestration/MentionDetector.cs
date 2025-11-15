using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quaaly.Infrastructure.Options;

namespace Quaaly.Worker.Orchestration;

/// <summary>
/// Service for detecting when the AI bot is @mentioned in pull request comments.
/// </summary>
public sealed class MentionDetector(ILogger<MentionDetector> logger, IOptionsMonitor<ReviewerOptions> options)
{
    private readonly ReviewerOptions _options = options.CurrentValue;

    /// <summary>
    /// Checks if the bot is mentioned in a comment using various @mention patterns.
    /// </summary>
    /// <param name="commentText">The comment text to check.</param>
    /// <param name="botDisplayName">Optional override for bot display name (uses config if not provided).</param>
    /// <returns>True if the bot is mentioned, false otherwise.</returns>
    public bool IsBotMentioned(string commentText, string? botDisplayName = null)
    {
        if (string.IsNullOrWhiteSpace(commentText))
        {
            return false;
        }

        var displayName = botDisplayName ?? _options.Queue.BotDisplayName;
        
        if (string.IsNullOrWhiteSpace(displayName))
        {
            logger.LogWarning("Bot display name is not configured, cannot detect @mentions");
            return false;
        }

        // Check for various @mention patterns (case-insensitive)
        var patterns = new[]
        {
            $"@{displayName}",                           // @Quaaly
            $"@{displayName.Replace(" ", "")}",         // @Quaaly (no spaces)
            $"@ {displayName}",                          // @ Quaaly (with space)
        };

        var mentioned = patterns.Any(pattern =>
            commentText.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        if (mentioned)
        {
            logger.LogDebug("Bot mention detected in comment: {Preview}",
                commentText.Length > 100 ? commentText[..100] + "..." : commentText);
        }

        return mentioned;
    }

    /// <summary>
    /// Extracts the portion of the comment text after the bot mention.
    /// Useful for understanding the user's request to the bot.
    /// </summary>
    /// <param name="commentText">The comment text containing the mention.</param>
    /// <param name="botDisplayName">Optional override for bot display name.</param>
    /// <returns>The text after the mention, or the full text if no mention is found.</returns>
    public string GetTextAfterMention(string commentText, string? botDisplayName = null)
    {
        if (string.IsNullOrWhiteSpace(commentText))
        {
            return string.Empty;
        }

        var displayName = botDisplayName ?? _options.Queue.BotDisplayName;
        
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return commentText;
        }

        // Find the @mention pattern
        var patterns = new[]
        {
            $"@{displayName}",
            $"@{displayName.Replace(" ", "")}",
            $"@ {displayName}",
        };

        foreach (var pattern in patterns)
        {
            var index = commentText.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                // Return everything after the mention pattern
                var afterMention = commentText.Substring(index + pattern.Length).Trim();
                
                // Remove common separators at the start
                afterMention = afterMention.TrimStart(':', ',', '-', '–');
                
                return afterMention.Trim();
            }
        }

        // No mention found, return full text
        return commentText;
    }
}
