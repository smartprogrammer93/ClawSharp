namespace ClawSharp.Core.Memory;

/// <summary>
/// Persistent memory store with CRUD, text search, and vector search capabilities.
/// </summary>
public interface IMemoryStore
{
    /// <summary>Store or update a memory entry.</summary>
    Task StoreAsync(string key, string content, MemoryCategory category = MemoryCategory.Core, CancellationToken ct = default);

    /// <summary>Retrieve a memory entry by key.</summary>
    Task<MemoryEntry?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Delete a memory entry by key.</summary>
    Task<bool> DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>Full-text search across memory entries.</summary>
    Task<IReadOnlyList<MemoryEntry>> SearchAsync(string query, int limit = 10, CancellationToken ct = default);

    /// <summary>Vector/semantic similarity search.</summary>
    Task<IReadOnlyList<MemoryEntry>> VectorSearchAsync(string query, int limit = 5, CancellationToken ct = default);

    /// <summary>List entries, optionally filtered by category.</summary>
    Task<IReadOnlyList<MemoryEntry>> ListAsync(MemoryCategory? category = null, int limit = 50, CancellationToken ct = default);

    /// <summary>Count total entries in the store.</summary>
    Task<int> CountAsync(CancellationToken ct = default);
}
