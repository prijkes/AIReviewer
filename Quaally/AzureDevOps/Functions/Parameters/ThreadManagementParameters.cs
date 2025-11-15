using System.ComponentModel;

namespace Quaally.AzureDevOps.Functions.Parameters;

/// <summary>
/// Parameters for creating a new comment thread on a pull request.
/// </summary>
public class CreateThreadParameters
{
    /// <summary>
    /// The file path where the comment should be placed (e.g., "src/Program.cs").
    /// </summary>
    [Description("The file path where the comment should be placed")]
    public string? FilePath { get; set; }

    /// <summary>
    /// The line number where the comment should start (1-based).
    /// </summary>
    [Description("The line number where the comment should start (1-based)")]
    public int? LineStart { get; set; }

    /// <summary>
    /// The line number where the comment should end (1-based).
    /// </summary>
    [Description("The line number where the comment should end (1-based)")]
    public int? LineEnd { get; set; }

    /// <summary>
    /// The comment text content.
    /// </summary>
    [Description("The comment text content")]
    public string CommentText { get; set; } = string.Empty;

    /// <summary>
    /// Initial status of the thread (active, byDesign, closed, fixed, pending, unknown, wontFix).
    /// </summary>
    [Description("Initial status: active, byDesign, closed, fixed, pending, unknown, wontFix")]
    public string? Status { get; set; }
}

/// <summary>
/// Parameters for replying to an existing comment thread.
/// </summary>
public class ReplyToThreadParameters
{
    /// <summary>
    /// The ID of the thread to reply to.
    /// </summary>
    [Description("The ID of the thread to reply to")]
    public int ThreadId { get; set; }

    /// <summary>
    /// The comment text content.
    /// </summary>
    [Description("The comment text content")]
    public string CommentText { get; set; } = string.Empty;
}

/// <summary>
/// Parameters for updating a thread's status.
/// </summary>
public class UpdateThreadStatusParameters
{
    /// <summary>
    /// The ID of the thread to update.
    /// </summary>
    [Description("The ID of the thread to update")]
    public int ThreadId { get; set; }

    /// <summary>
    /// New status for the thread (active, byDesign, closed, fixed, pending, unknown, wontFix).
    /// </summary>
    [Description("New status: active, byDesign, closed, fixed, pending, unknown, wontFix")]
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Parameters for getting a thread's conversation history.
/// </summary>
public class GetThreadConversationParameters
{
    /// <summary>
    /// The ID of the thread to retrieve.
    /// </summary>
    [Description("The ID of the thread to retrieve")]
    public int ThreadId { get; set; }
}
