namespace ClawSharp.Core.Sessions;

/// <summary>
/// Manages conversation sessions with persistence.
/// </summary>
public interface ISessionManager
{
    /// <summary>Get an existing session or create a new one.</summary>
    Task<SessionContext> GetOrCreateAsync(string sessionKey, string channel, string chatId, CancellationToken ct = default);

    /// <summary>Save/update a session.</summary>
    Task SaveAsync(SessionContext session, CancellationToken ct = default);

    /// <summary>List all active sessions.</summary>
    Task<IReadOnlyList<SessionContext>> ListAsync(CancellationToken ct = default);

    /// <summary>Delete a session by key.</summary>
    Task DeleteAsync(string sessionKey, CancellationToken ct = default);
}
