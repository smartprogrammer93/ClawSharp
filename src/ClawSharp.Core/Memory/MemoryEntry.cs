namespace ClawSharp.Core.Memory;

/// <summary>
/// A single entry in the memory store.
/// </summary>
public record MemoryEntry(
    /// <summary>Unique identifier</summary>
    string Id,
    /// <summary>Lookup key</summary>
    string Key,
    /// <summary>Content text</summary>
    string Content,
    /// <summary>Category of this entry</summary>
    MemoryCategory Category,
    /// <summary>When the entry was created or last updated</summary>
    DateTimeOffset Timestamp,
    /// <summary>Optional session ID this entry belongs to</summary>
    string? SessionId = null,
    /// <summary>Optional relevance score from search</summary>
    double? Score = null
);
