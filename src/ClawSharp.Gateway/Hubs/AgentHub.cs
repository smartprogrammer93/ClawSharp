using ClawSharp.Agent;
using ClawSharp.Core.Channels;
using ClawSharp.Core.Providers;
using ClawSharp.Core.Sessions;
using Microsoft.AspNetCore.SignalR;
using CoreLlmMessage = ClawSharp.Core.Providers.LlmMessage;

namespace ClawSharp.Gateway.Hubs;

/// <summary>
/// SignalR hub for real-time agent ↔ UI communication.
/// </summary>
public class AgentHub : Hub
{
    private readonly AgentLoop _agentLoop;
    private readonly ISessionManager _sessionManager;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<AgentHub> _logger;

    public AgentHub(
        AgentLoop agentLoop,
        ISessionManager sessionManager,
        IMessageBus messageBus,
        ILogger<AgentHub> logger)
    {
        _agentLoop = agentLoop;
        _sessionManager = sessionManager;
        _messageBus = messageBus;
        _logger = logger;
    }

    /// <summary>
    /// Send a message to the agent and get a streaming response.
    /// </summary>
    /// <param name="message">The user's message</param>
    /// <param name="channel">The channel name (e.g., "web", "telegram")</param>
    /// <param name="chatId">The chat/session ID</param>
    /// <param name="model">Optional model to use</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task SendMessageAsync(
        string message,
        string channel,
        string chatId,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get or create session
            var session = await _sessionManager.GetOrCreateAsync(
                channel,
                chatId,
                Context.ConnectionId,
                cancellationToken);

            // Add user message to session history
            session.History.Add(new CoreLlmMessage("user", message));

            // Run agent loop
            var request = new AgentLoop.AgentRequest(
                Model: model ?? "default",
                InitialMessages: session.History
            );

            _logger.LogInformation("Processing message for session {SessionKey}", session.SessionKey);

            var result = await _agentLoop.RunAsync(request, cancellationToken);

            // Add assistant response to session history
            session.History.Add(new CoreLlmMessage("assistant", result.Content));
            await _sessionManager.SaveAsync(session, cancellationToken);

            // Send completion to client
            await Clients.Caller.SendAsync("OnComplete", result, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Message processing cancelled for connection {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("OnError", new OperationCanceledException("Request was cancelled"), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message for connection {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("OnError", ex, cancellationToken);
        }
    }

    /// <summary>
    /// Stream a message to the agent with streaming responses.
    /// </summary>
    public async Task SendMessageStreamAsync(
        string message,
        string channel,
        string chatId,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        IDisposable? toolStartedSubscription = null;
        IDisposable? toolCompletedSubscription = null;

        try
        {
            // Get or create session
            var session = await _sessionManager.GetOrCreateAsync(
                channel,
                chatId,
                Context.ConnectionId,
                cancellationToken);

            // Add user message to session history
            session.History.Add(new CoreLlmMessage("user", message));

            // Run agent loop with streaming
            var request = new AgentLoop.AgentRequest(
                Model: model ?? "default",
                InitialMessages: session.History
            );

            _logger.LogInformation("Streaming message for session {SessionKey}", session.SessionKey);

            // Subscribe to tool events and forward to client
            toolStartedSubscription = _messageBus.Subscribe<AgentLoop.ToolStartedEvent>(async (evt) =>
            {
                await Clients.Caller.SendAsync("OnToolStarted", evt);
            });

            toolCompletedSubscription = _messageBus.Subscribe<AgentLoop.ToolCompletedEvent>(async (evt) =>
            {
                await Clients.Caller.SendAsync("OnToolCompleted", evt);
            });

            var result = await _agentLoop.RunAsync(request, cancellationToken);

            // Add assistant response to session history
            session.History.Add(new CoreLlmMessage("assistant", result.Content));
            await _sessionManager.SaveAsync(session, cancellationToken);

            // Send completion to client
            await Clients.Caller.SendAsync("OnComplete", result, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Message streaming cancelled for connection {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("OnError", new OperationCanceledException("Request was cancelled"), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming message for connection {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("OnError", ex, cancellationToken);
        }
        finally
        {
            // Unsubscribe from events
            toolStartedSubscription?.Dispose();
            toolCompletedSubscription?.Dispose();
        }
    }

    /// <summary>
    /// Cancel the current request.
    /// </summary>
    public Task CancelAsync()
    {
        // SignalR doesn't have built-in per-request cancellation
        // The cancellation token passed to SendMessage/SendMessageStream controls this
        _logger.LogInformation("Cancel requested for connection {ConnectionId}", Context.ConnectionId);
        return Task.CompletedTask;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
