namespace Quaaly.Core.Models;

/// <summary>
/// Represents a user identity across source control providers.
/// </summary>
public sealed class UserIdentity
{
    /// <summary>
    /// Unique identifier for the user.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name of the user.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Email address of the user.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Username or unique name.
    /// </summary>
    public string? UniqueName { get; init; }

    /// <summary>
    /// Avatar/profile image URL.
    /// </summary>
    public string? AvatarUrl { get; init; }
}
