using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AIReviewer.AzureDevOps;

/// <summary>
/// Extension methods for GitPullRequestCommentThread to improve readability and reduce duplication.
/// </summary>
public static class GitPullRequestExtensions
{
    /// <summary>Property key to identify bot-created threads.</summary>
    private const string BotProperty = "ai-bot";
    
    /// <summary>Property key to store issue fingerprints for tracking across iterations.</summary>
    private const string FingerprintProperty = "fingerprint";
    
    /// <summary>Property key to store the iteration ID when the thread was created.</summary>
    private const string IterationIdProperty = "iteration-id";
    
    /// <summary>Identifier for the special state tracking thread.</summary>
    private const string StateThreadIdentifier = "ai-state";

    /// <summary>
    /// Determines if a comment thread was created by the AI bot.
    /// </summary>
    /// <param name="thread">The comment thread to check.</param>
    /// <returns>True if the thread was created by the bot; otherwise false.</returns>
    public static bool IsCreatedByBot(this GitPullRequestCommentThread thread)
    {
        return thread.Properties?.ContainsKey(BotProperty) == true;
    }

    /// <summary>
    /// Gets the fingerprint value from a comment thread.
    /// </summary>
    /// <param name="thread">The comment thread to get the fingerprint from.</param>
    /// <returns>The fingerprint string if it exists; otherwise null.</returns>
    public static string? GetFingerprint(this GitPullRequestCommentThread thread)
    {
        return thread.Properties?.TryGetValue(FingerprintProperty, out var fp) == true
            ? fp.ToString()
            : null;
    }

    /// <summary>
    /// Sets the fingerprint value on a comment thread.
    /// </summary>
    /// <param name="thread">The comment thread to set the fingerprint on.</param>
    /// <param name="fingerprint">The fingerprint value to set.</param>
    public static void SetFingerprint(this GitPullRequestCommentThread thread, string fingerprint)
    {
        thread.Properties ??= [];
        thread.Properties[FingerprintProperty] = fingerprint;
    }

    /// <summary>
    /// Determines if a comment thread is the special state tracking thread.
    /// </summary>
    /// <param name="thread">The comment thread to check.</param>
    /// <returns>True if the thread is the state thread; otherwise false.</returns>
    public static bool IsStateThread(this GitPullRequestCommentThread thread)
    {
        return thread.Properties?.ContainsKey(StateThreadIdentifier) == true;
    }

    /// <summary>
    /// Marks a comment thread as a bot-created thread.
    /// </summary>
    /// <param name="thread">The comment thread to mark.</param>
    public static void MarkAsBot(this GitPullRequestCommentThread thread)
    {
        thread.Properties ??= [];
        thread.Properties[BotProperty] = true;
    }

    /// <summary>
    /// Marks a comment thread as the state tracking thread.
    /// </summary>
    /// <param name="thread">The comment thread to mark.</param>
    public static void MarkAsStateThread(this GitPullRequestCommentThread thread)
    {
        thread.Properties ??= [];
        thread.Properties[BotProperty] = true;
        thread.Properties[StateThreadIdentifier] = true;
    }

    /// <summary>
    /// Gets the iteration ID from a comment thread.
    /// </summary>
    /// <param name="thread">The comment thread to get the iteration ID from.</param>
    /// <returns>The iteration ID if it exists; otherwise null.</returns>
    public static int? GetIterationId(this GitPullRequestCommentThread thread)
    {
        if (thread.Properties?.TryGetValue(IterationIdProperty, out var iterationId) == true)
        {
            if (iterationId is int intValue)
                return intValue;
            if (int.TryParse(iterationId.ToString(), out var parsedValue))
                return parsedValue;
        }
        return null;
    }

    /// <summary>
    /// Sets the iteration ID on a comment thread.
    /// </summary>
    /// <param name="thread">The comment thread to set the iteration ID on.</param>
    /// <param name="iterationId">The iteration ID to set.</param>
    public static void SetIterationId(this GitPullRequestCommentThread thread, int iterationId)
    {
        thread.Properties ??= [];
        thread.Properties[IterationIdProperty] = iterationId;
    }
}
