using ClawSharp.Core.Sessions;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace ClawSharp.Agent;

/// <summary>
/// SQLite-backed session manager with conversation history persistence.
/// </summary>
public class SqliteSessionManager : ISessionManager, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _connection;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private bool _disposed;

    public SqliteSessionManager(string connectionString)
    {
        // Handle various connection string formats
        _connectionString = connectionString switch
        {
            ":memory:" => "Data Source=file::memory:?cache=shared",
            _ when !connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) 
                => $"Data Source={connectionString}",
            _ => connectionString
        };
        
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
        InitializeSchema();
    }

    private void InitializeSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS sessions (
                session_key TEXT PRIMARY KEY,
                channel TEXT NOT NULL,
                chat_id TEXT NOT NULL,
                history_json TEXT NOT NULL DEFAULT '[]',
                summary TEXT,
                created TEXT NOT NULL,
                last_active TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_sessions_channel_chat ON sessions(channel, chat_id);
            """;
        cmd.ExecuteNonQuery();
    }

    public async Task<SessionContext> GetOrCreateAsync(string sessionKey, string channel, string chatId, CancellationToken ct = default)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT session_key, channel, chat_id, history_json, summary, created, last_active 
            FROM sessions WHERE session_key = @sessionKey
            """;
        cmd.Parameters.AddWithValue("@sessionKey", sessionKey);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadSession(reader);
        }
        
        // Create new session
        var now = DateTimeOffset.UtcNow.ToString("O");
        await using var insertCmd = _connection.CreateCommand();
        insertCmd.CommandText = """
            INSERT INTO sessions (session_key, channel, chat_id, history_json, created, last_active)
            VALUES (@sessionKey, @channel, @chatId, @historyJson, @created, @lastActive)
            """;
        insertCmd.Parameters.AddWithValue("@sessionKey", sessionKey);
        insertCmd.Parameters.AddWithValue("@channel", channel);
        insertCmd.Parameters.AddWithValue("@chatId", chatId);
        insertCmd.Parameters.AddWithValue("@historyJson", "[]");
        insertCmd.Parameters.AddWithValue("@created", now);
        insertCmd.Parameters.AddWithValue("@lastActive", now);
        
        await insertCmd.ExecuteNonQueryAsync(ct);
        
        return new SessionContext
        {
            SessionKey = sessionKey,
            Channel = channel,
            ChatId = chatId,
            History = [],
            Created = DateTimeOffset.UtcNow,
            LastActive = DateTimeOffset.UtcNow
        };
    }

    public async Task SaveAsync(SessionContext session, CancellationToken ct = default)
    {
        var historyJson = JsonSerializer.Serialize(session.History, _jsonOptions);
        var now = DateTimeOffset.UtcNow.ToString("O");
        
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sessions (session_key, channel, chat_id, history_json, summary, created, last_active)
            VALUES (@sessionKey, @channel, @chatId, @historyJson, @summary, @created, @lastActive)
            ON CONFLICT(session_key) DO UPDATE SET
                history_json = @historyJson,
                summary = @summary,
                last_active = @lastActive
            """;
        cmd.Parameters.AddWithValue("@sessionKey", session.SessionKey);
        cmd.Parameters.AddWithValue("@channel", session.Channel);
        cmd.Parameters.AddWithValue("@chatId", session.ChatId);
        cmd.Parameters.AddWithValue("@historyJson", historyJson);
        cmd.Parameters.AddWithValue("@summary", session.Summary ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@created", session.Created.ToString("O"));
        cmd.Parameters.AddWithValue("@lastActive", now);
        
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<SessionContext>> ListAsync(CancellationToken ct = default)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT session_key, channel, chat_id, history_json, summary, created, last_active 
            FROM sessions ORDER BY last_active DESC
            """;
        
        var results = new List<SessionContext>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadSession(reader));
        }
        return results;
    }

    public async Task DeleteAsync(string sessionKey, CancellationToken ct = default)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM sessions WHERE session_key = @sessionKey";
        cmd.Parameters.AddWithValue("@sessionKey", sessionKey);
        
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Clears all sessions. Useful for testing.
    /// </summary>
    public async Task ClearAllAsync(CancellationToken ct = default)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM sessions";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private SessionContext ReadSession(SqliteDataReader reader)
    {
        var sessionKey = reader.GetString(0);
        var channel = reader.GetString(1);
        var chatId = reader.GetString(2);
        var historyJson = reader.GetString(3);
        var summary = reader.IsDBNull(4) ? null : reader.GetString(4);
        var created = DateTimeOffset.Parse(reader.GetString(5));
        var lastActive = DateTimeOffset.Parse(reader.GetString(6));
        
        var history = JsonSerializer.Deserialize<List<ClawSharp.Core.Providers.LlmMessage>>(historyJson, _jsonOptions) ?? [];
        
        return new SessionContext
        {
            SessionKey = sessionKey,
            Channel = channel,
            ChatId = chatId,
            History = history,
            Summary = summary,
            Created = created,
            LastActive = lastActive
        };
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
