namespace Quaaly.Core.Enums;

/// <summary>
/// Type of change made to a file.
/// </summary>
public enum FileChangeType
{
    /// <summary>
    /// File was added.
    /// </summary>
    Add,

    /// <summary>
    /// File was modified.
    /// </summary>
    Edit,

    /// <summary>
    /// File was deleted.
    /// </summary>
    Delete,

    /// <summary>
    /// File was renamed.
    /// </summary>
    Rename,

    /// <summary>
    /// No change.
    /// </summary>
    None
}
