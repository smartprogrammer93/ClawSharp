using ClawSharp.Agent;
using ClawSharp.Core.Providers;
using ClawSharp.Core.Sessions;
using FluentAssertions;

namespace ClawSharp.Agent.Tests;

public class SessionManagerTests : IAsyncDisposable
{
    // Use unique temp file for each test instance to avoid parallel test pollution
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
    private readonly SqliteSessionManager _manager;

    public SessionManagerTests()
    {
        _manager = new SqliteSessionManager(_dbPath);
    }

    [Fact]
    public async Task GetOrCreateAsync_NewSession_CreatesIt()
    {
        var session = await _manager.GetOrCreateAsync("test:123", "telegram", "123");
        session.Should().NotBeNull();
        session.SessionKey.Should().Be("test:123");
        session.Channel.Should().Be("telegram");
        session.History.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrCreateAsync_ExistingSession_ReturnsIt()
    {
        await _manager.GetOrCreateAsync("test:123", "telegram", "123");
        var session = await _manager.GetOrCreateAsync("test:123", "telegram", "123");
        session.SessionKey.Should().Be("test:123");
    }

    [Fact]
    public async Task SaveAsync_PersistsHistory()
    {
        var session = await _manager.GetOrCreateAsync("test:123", "telegram", "123");
        session.History.Add(new LlmMessage("user", "Hello"));
        session.History.Add(new LlmMessage("assistant", "Hi!"));
        await _manager.SaveAsync(session);
        var loaded = await _manager.GetOrCreateAsync("test:123", "telegram", "123");
        loaded.History.Should().HaveCount(2);
        loaded.History[0].Content.Should().Be("Hello");
    }

    [Fact]
    public async Task DeleteAsync_RemovesSession()
    {
        await _manager.GetOrCreateAsync("test:123", "telegram", "123");
        await _manager.DeleteAsync("test:123");
        var sessions = await _manager.ListAsync();
        sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_ReturnsAllSessions()
    {
        await _manager.GetOrCreateAsync("s1", "telegram", "1");
        await _manager.GetOrCreateAsync("s2", "discord", "2");
        var sessions = await _manager.ListAsync();
        sessions.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveAsync_UpdatesExistingSession()
    {
        var session = await _manager.GetOrCreateAsync("test:123", "telegram", "123");
        session.History.Add(new LlmMessage("user", "Hello"));
        await _manager.SaveAsync(session);
        
        // GetOrCreate returns the existing session with its history from DB
        // Clear it first then add new messages
        var session2 = await _manager.GetOrCreateAsync("test:123", "telegram", "123");
        session2.History.Clear(); // Clear existing history from DB
        session2.History.Add(new LlmMessage("user", "Hello"));
        session2.History.Add(new LlmMessage("assistant", "Hi there!"));
        await _manager.SaveAsync(session2);
        
        var loaded = await _manager.GetOrCreateAsync("test:123", "telegram", "123");
        loaded.History.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveAsync_PersistsSummary()
    {
        var session = await _manager.GetOrCreateAsync("test:123", "telegram", "123");
        session.Summary = "Discussed weather and sports";
        await _manager.SaveAsync(session);
        
        var loaded = await _manager.GetOrCreateAsync("test:123", "telegram", "123");
        loaded.Summary.Should().Be("Discussed weather and sports");
    }

    public async ValueTask DisposeAsync()
    {
        await ((IAsyncDisposable)_manager).DisposeAsync();
        try { File.Delete(_dbPath); } catch { }
    }
}
