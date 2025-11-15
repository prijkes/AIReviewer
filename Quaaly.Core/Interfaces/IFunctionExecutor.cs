namespace Quaaly.Core.Interfaces;

/// <summary>
/// Interface for executing AI function calls in a provider-agnostic way.
/// </summary>
public interface IFunctionExecutor
{
    /// <summary>
    /// Sets the context for function execution (repository and pull request).
    /// </summary>
    /// <param name="repositoryId">Repository identifier.</param>
    /// <param name="pullRequestId">Pull request identifier.</param>
    void SetContext(string repositoryId, int pullRequestId);

    /// <summary>
    /// Executes a function by name with the provided arguments.
    /// </summary>
    /// <param name="functionName">Name of the function to execute.</param>
    /// <param name="argumentsJson">Function arguments as JSON string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Function execution result as string.</returns>
    Task<string> ExecuteFunctionAsync(
        string functionName,
        string argumentsJson,
        CancellationToken cancellationToken = default);
}
