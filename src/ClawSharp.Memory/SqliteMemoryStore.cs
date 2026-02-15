using Microsoft.Data.Sqlite;
using ClawSharp.Core.Memory;

namespace ClawSharp.Memory;

/// <summary>
/// SQLite-backed memory store with FTS5 full-text search.
/// </summary>
public class SqliteMemoryStore : IMemoryStore, IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public SqliteMemoryStore(string connectionString)
    {
        // Handle both full connection strings and ":memory:" shorthand
        var actualConnectionString = connectionString == ":memory:" 
            ? "Data Source=:memory:" 
            : connectionString;
        
        _connection = new SqliteConnection(actualConnectionString);
        _connection.Open();
        InitializeSchema();
    }

    private void InitializeSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS memories (
                id TEXT PRIMARY KEY,
                key TEXT NOT NULL UNIQUE,
                content TEXT NOT NULL,
                category INTEGER NOT NULL,
                session_id TEXT,
                timestamp TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_memories_key ON memories(key);
            CREATE INDEX IF NOT EXISTS idx_memories_category ON memories(category);
            CREATE INDEX IF NOT EXISTS idx_memories_timestamp ON memories(timestamp DESC);
            
            CREATE VIRTUAL TABLE IF NOT EXISTS memories_fts USING fts5(
                key,
                content,
                content='memories',
                content_rowid='rowid',
                tokenize='unicode61'
            );
            
            CREATE TRIGGER IF NOT EXISTS memories_ai AFTER INSERT ON memories BEGIN
                INSERT INTO memories_fts(rowid, key, content) VALUES (new.rowid, new.key, new.content);
            END;
            
            CREATE TRIGGER IF NOT EXISTS memories_ad AFTER DELETE ON memories BEGIN
                INSERT INTO memories_fts(memories_fts, rowid, key, content) VALUES('delete', old.rowid, old.key, old.content);
            END;
            
            CREATE TRIGGER IF NOT EXISTS memories_au AFTER UPDATE ON memories BEGIN
                INSERT INTO memories_fts(memories_fts, rowid, key, content) VALUES('delete', old.rowid, old.key, old.content);
                INSERT INTO memories_fts(rowid, key, content) VALUES (new.rowid, new.key, new.content);
            END;
            """;
        cmd.ExecuteNonQuery();
    }

    public async Task StoreAsync(string key, string content, MemoryCategory category = MemoryCategory.Core, CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow.ToString("O");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO memories (id, key, content, category, timestamp)
            VALUES (@id, @key, @content, @category, @timestamp)
            ON CONFLICT(key) DO UPDATE SET
                content = @content,
                category = @category,
                timestamp = @timestamp
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.Parameters.AddWithValue("@category", (int)category);
        cmd.Parameters.AddWithValue("@timestamp", timestamp);
        
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<MemoryEntry?> GetAsync(string key, CancellationToken ct = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, key, content, category, timestamp, session_id FROM memories WHERE key = @key";
        cmd.Parameters.AddWithValue("@key", key);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadEntry(reader);
        }
        return null;
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM memories WHERE key = @key";
        cmd.Parameters.AddWithValue("@key", key);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<IReadOnlyList<MemoryEntry>> SearchAsync(string query, int limit = 10, CancellationToken ct = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT m.id, m.key, m.content, m.category, m.timestamp, m.session_id,
                   bm25(memories_fts) as score
            FROM memories_fts
            JOIN memories m ON memories_fts.rowid = m.rowid
            WHERE memories_fts MATCH @query
            ORDER BY score
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@query", query);
        cmd.Parameters.AddWithValue("@limit", limit);

        var results = new List<MemoryEntry>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetString(0);
            var key = reader.GetString(1);
            var content = reader.GetString(2);
            var category = (MemoryCategory)reader.GetInt32(3);
            var timestamp = DateTimeOffset.Parse(reader.GetString(4));
            var sessionId = reader.IsDBNull(5) ? null : reader.GetString(5);
            var score = reader.GetDouble(6);
            
            results.Add(new MemoryEntry(id, key, content, category, timestamp, sessionId, score));
        }
        return results;
    }

    public Task<IReadOnlyList<MemoryEntry>> VectorSearchAsync(string query, int limit = 5, CancellationToken ct = default)
    {
        // Vector search is a stub - returns empty results
        return Task.FromResult<IReadOnlyList<MemoryEntry>>(Array.Empty<MemoryEntry>());
    }

    public async Task<IReadOnlyList<MemoryEntry>> ListAsync(MemoryCategory? category = null, int limit = 50, CancellationToken ct = default)
    {
        using var cmd = _connection.CreateCommand();
        
        if (category.HasValue)
        {
            cmd.CommandText = """
                SELECT id, key, content, category, timestamp, session_id 
                FROM memories 
                WHERE category = @category 
                ORDER BY timestamp DESC 
                LIMIT @limit
                """;
            cmd.Parameters.AddWithValue("@category", (int)category.Value);
        }
        else
        {
            cmd.CommandText = """
                SELECT id, key, content, category, timestamp, session_id 
                FROM memories 
                ORDER BY timestamp DESC 
                LIMIT @limit
                """;
        }
        cmd.Parameters.AddWithValue("@limit", limit);

        var results = new List<MemoryEntry>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadEntry(reader));
        }
        return results;
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM memories";
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    private static MemoryEntry ReadEntry(SqliteDataReader reader)
    {
        return new MemoryEntry(
            Id: reader.GetString(0),
            Key: reader.GetString(1),
            Content: reader.GetString(2),
            Category: (MemoryCategory)reader.GetInt32(3),
            Timestamp: DateTimeOffset.Parse(reader.GetString(4)),
            SessionId: reader.IsDBNull(5) ? null : reader.GetString(5)
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
            _disposed = true;
        }
    }
}
