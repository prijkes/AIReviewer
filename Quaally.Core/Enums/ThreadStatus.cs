namespace Quaally.Core.Enums;

/// <summary>
/// Status of a comment thread.
/// </summary>
public enum ThreadStatus
{
    /// <summary>
    /// Thread is active and requires attention.
    /// </summary>
    Active,

    /// <summary>
    /// Thread has been fixed.
    /// </summary>
    Fixed,

    /// <summary>
    /// Thread is closed/resolved.
    /// </summary>
    Closed,

    /// <summary>
    /// Thread is marked as by design (won't fix).
    /// </summary>
    ByDesign,

    /// <summary>
    /// Thread is pending review.
    /// </summary>
    Pending,

    /// <summary>
    /// Thread is marked as won't fix.
    /// </summary>
    WontFix
}
