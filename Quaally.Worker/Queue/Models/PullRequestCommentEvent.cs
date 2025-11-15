using System.Text.Json.Serialization;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace Quaally.Worker.Queue.Models;

/// <summary>
/// Represents a pull request comment event from Azure DevOps.
/// Triggered when a comment is added, edited, or deleted on a pull request.
/// Event type: "ms.vss-code.git-pullrequest-comment-event"
/// Only includes properties actually used by the application.
/// </summary>
public class PullRequestCommentEventResource
{
    /// <summary>
    /// The comment that was added or modified.
    /// </summary>
    [JsonPropertyName("comment")]
    public CommentInfo? Comment { get; set; }

    /// <summary>
    /// The pull request that the comment is associated with.
    /// </summary>
    [JsonPropertyName("pullRequest")]
    public GitPullRequest? PullRequest { get; set; }
}

/// <summary>
/// Information about a comment in a pull request thread.
/// Only includes properties actually used by the orchestrator.
/// </summary>
public class CommentInfo
{
    /// <summary>
    /// Unique identifier for the comment.
    /// Used to locate the thread containing this comment.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Parent comment ID if this is a reply.
    /// Used to determine if we should retrieve thread conversation history.
    /// </summary>
    [JsonPropertyName("parentCommentId")]
    public int? ParentCommentId { get; set; }

    /// <summary>
    /// The text content of the comment.
    /// Used for @mention detection and extracting the user's request.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}
