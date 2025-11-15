namespace Quaally.AI;

/// <summary>
/// Default values and limits for AI function calling parameters.
/// Centralizes magic numbers to ensure consistency across parameter classes and implementations.
/// </summary>
public static class FunctionDefaults
{
    /// <summary>
    /// Default maximum number of search results to return.
    /// </summary>
    public const int SearchDefaultMaxResults = 10;

    /// <summary>
    /// Maximum limit for search results.
    /// </summary>
    public const int SearchMaxResultsLimit = 100;

    /// <summary>
    /// Default maximum number of commits to return in file history.
    /// </summary>
    public const int FileHistoryDefaultMaxCommits = 5;

    /// <summary>
    /// Maximum limit for file history commits.
    /// </summary>
    public const int FileHistoryMaxCommitsLimit = 30;
}
