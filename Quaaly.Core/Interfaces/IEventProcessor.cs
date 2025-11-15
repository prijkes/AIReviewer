using Quaaly.Core.Models;

namespace Quaaly.Core.Interfaces;

/// <summary>
/// Interface for processing events from source control providers.
/// </summary>
public interface IEventProcessor
{
    /// <summary>
    /// Processes a comment event from the source control provider.
    /// </summary>
    /// <param name="commentEvent">The comment event to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task ProcessCommentEventAsync(CommentEvent commentEvent, CancellationToken cancellationToken = default);
}
