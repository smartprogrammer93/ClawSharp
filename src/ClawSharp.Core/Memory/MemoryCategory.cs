namespace ClawSharp.Core.Memory;

/// <summary>
/// Category of a memory entry.
/// </summary>
public enum MemoryCategory
{
    /// <summary>Core/permanent memories</summary>
    Core,
    /// <summary>Daily log entries</summary>
    Daily,
    /// <summary>Conversation-scoped memories</summary>
    Conversation,
    /// <summary>User-defined custom category</summary>
    Custom
}
