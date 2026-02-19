using ClawSharp.Core.Channels;
using ClawSharp.Core.Providers;
using ClawSharp.Core.Tools;
using Microsoft.Extensions.Logging;

namespace ClawSharp.Agent;

/// <summary>
/// Request to spawn a sub-agent.
/// </summary>
public record SubAgentRequest(
    string Task,
    string? Model = null,
    string? SystemPrompt = null,
    int MaxIterations = 10
);

/// <summary>
/// Result from a sub-agent execution.
/// </summary>
public record SubAgentResult(
    string SessionId,
    bool Success,
    string? Content = null,
    string? Error = null,
    DateTimeOffset StartedAt = default,
    DateTimeOffset CompletedAt = default
);

/// <summary>
/// Factory for spawning isolated sub-agent instances for parallel task execution.
/// Each sub-agent runs in its own session with its own AgentLoop.
/// </summary>
public sealed class SubAgentFactory
{
    private readonly ILlmProvider _provider;
    private readonly IToolRegistry _tools;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<SubAgentFactory> _logger;
    private readonly int _maxConcurrent;
    private int _activeCount;
    private readonly object _lock = new();
    private readonly List<SubAgentResult> _completedSessions = [];

    /// <summary>Number of currently running sub-agents.</summary>
    public int ActiveCount => _activeCount;

    /// <summary>Maximum concurrent sub-agents allowed.</summary>
    public int MaxConcurrent => _maxConcurrent;

    /// <summary>History of completed sub-agent sessions.</summary>
    public IReadOnlyList<SubAgentResult> CompletedSessions
    {
        get { lock (_lock) return [.. _completedSessions]; }
    }

    public SubAgentFactory(
        ILlmProvider provider,
        IToolRegistry tools,
        IMessageBus messageBus,
        ILogger<SubAgentFactory> logger,
        int maxConcurrent = 5)
    {
        _provider = provider;
        _tools = tools;
        _messageBus = messageBus;
        _logger = logger;
        _maxConcurrent = maxConcurrent;
    }

    /// <summary>
    /// Spawn a sub-agent to execute a task.
    /// </summary>
    public async Task<SubAgentResult> SpawnAsync(SubAgentRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (_activeCount >= _maxConcurrent)
                throw new InvalidOperationException(
                    $"Max concurrent sub-agents ({_maxConcurrent}) reached. Wait for existing agents to complete.");
            _activeCount++;
        }

        var sessionId = $"subagent:{Guid.NewGuid():N}";
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogInformation("Spawning sub-agent {SessionId} for task: {Task}",
                sessionId, Truncate(request.Task, 100));

            var agentLogger = new SubAgentLogger<AgentLoop>(_logger);
            
            var agentLoop = new AgentLoop(
                _provider,
                _tools,
                _messageBus,
                agentLogger,
                request.MaxIterations);

            var messages = BuildMessages(request);
            var agentRequest = new AgentLoop.AgentRequest(
                request.Model ?? "default",
                messages);

            var agentResult = await agentLoop.RunAsync(agentRequest, ct);

            var result = new SubAgentResult(
                sessionId,
                Success: true,
                Content: agentResult.Content,
                StartedAt: startedAt,
                CompletedAt: DateTimeOffset.UtcNow);

            lock (_lock) _completedSessions.Add(result);
            _logger.LogInformation("Sub-agent {SessionId} completed successfully", sessionId);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sub-agent {SessionId} failed", sessionId);
            var result = new SubAgentResult(
                sessionId,
                Success: false,
                Error: ex.Message,
                StartedAt: startedAt,
                CompletedAt: DateTimeOffset.UtcNow);

            lock (_lock) _completedSessions.Add(result);
            return result;
        }
        finally
        {
            lock (_lock) _activeCount--;
        }
    }

    private static List<LlmMessage> BuildMessages(SubAgentRequest request)
    {
        var messages = new List<LlmMessage>();

        var systemPrompt = request.SystemPrompt ?? "You are a sub-agent. Complete the assigned task concisely.";
        messages.Add(new LlmMessage("system", systemPrompt));
        messages.Add(new LlmMessage("user", request.Task));

        return messages;
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + "...";

    // Simple logger wrapper that forwards to parent logger
    private sealed class SubAgentLogger<T>(ILogger parentLogger) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => parentLogger.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => parentLogger.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => parentLogger.Log(logLevel, eventId, state, exception, formatter);
    }
}
