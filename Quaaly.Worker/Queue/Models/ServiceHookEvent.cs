using System.Text.Json.Serialization;

namespace Quaaly.Worker.Queue.Models;

/// <summary>
/// Base class for all Azure DevOps service hook events.
/// Represents the minimal structure needed to process webhook payloads.
/// Only includes properties actually used by the application.
/// </summary>
public class ServiceHookEvent
{
    /// <summary>
    /// The type of event (e.g., "git.pullrequest.created", "ms.vss-code.git-pullrequest-comment-event").
    /// Used to route the event to the appropriate handler.
    /// </summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// The resource affected by this event (deserialized based on event type).
    /// Contains the actual pull request, comment, or other data specific to the event.
    /// </summary>
    [JsonPropertyName("resource")]
    public object? Resource { get; set; }
}
