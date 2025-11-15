using Quaaly.Core.Enums;
using Quaaly.Core.Models;

namespace Quaaly.Core.Interfaces;

/// <summary>
/// Interface for managing comments and threads on pull requests.
/// </summary>
public interface ICommentService
{
    /// <summary>
    /// Creates a new comment thread on the pull request.
    /// </summary>
    /// <param name="content">Content of the comment.</param>
    /// <param name="filePath">Optional file path for inline comments.</param>
    /// <param name="lineStart">Optional starting line number for inline comments.</param>
    /// <param name="lineEnd">Optional ending line number for inline comments.</param>
    /// <param name="status">Initial status of the thread.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created thread.</returns>
    Task<ReviewThread> CreateThreadAsync(
        string content,
        string? filePath = null,
        int? lineStart = null,
        int? lineEnd = null,
        ThreadStatus status = ThreadStatus.Active,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a reply to an existing thread.
    /// </summary>
    /// <param name="threadId">ID of the thread to reply to.</param>
    /// <param name="content">Content of the reply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created comment.</returns>
    Task<Comment> ReplyToThreadAsync(
        int threadId,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of a thread.
    /// </summary>
    /// <param name="threadId">ID of the thread to update.</param>
    /// <param name="status">New status for the thread.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task UpdateThreadStatusAsync(
        int threadId,
        ThreadStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all threads for the pull request.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all threads.</returns>
    Task<List<ReviewThread>> GetThreadsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific thread by ID.
    /// </summary>
    /// <param name="threadId">ID of the thread.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The thread, or null if not found.</returns>
    Task<ReviewThread?> GetThreadAsync(int threadId, CancellationToken cancellationToken = default);
}
