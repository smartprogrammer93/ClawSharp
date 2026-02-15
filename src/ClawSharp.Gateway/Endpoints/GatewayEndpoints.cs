using ClawSharp.Agent;
using ClawSharp.Core.Channels;
using ClawSharp.Core.Config;
using ClawSharp.Core.Providers;
using ClawSharp.Core.Sessions;
using ClawSharp.Infrastructure.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ClawSharp.Gateway.Endpoints;

/// <summary>
/// Request model for chat endpoint.
/// </summary>
public record ChatRequest(
    string Message,
    string? SessionKey = null,
    string? Model = null
);

/// <summary>
/// Response model for chat endpoint.
/// </summary>
public record ChatResponse(
    string Response,
    string? SessionKey,
    IReadOnlyList<ToolExecutionInfo>? ToolExecutions = null
);

/// <summary>
/// Tool execution info for response.
/// </summary>
public record ToolExecutionInfo(
    string ToolCallId,
    string ToolName,
    string ArgumentsJson,
    bool Success,
    string? Output,
    string? Error
);

/// <summary>
/// Request model for messages endpoint.
/// </summary>
public record MessageRequest(
    string Channel,
    string ChatId,
    string Content
);

/// <summary>
/// Minimal API endpoints for the ClawSharp HTTP gateway.
/// </summary>
public static class GatewayEndpoints
{
    public static void MapGatewayEndpoints(this WebApplication app)
    {
        var logger = app.Logger;

        // Health check endpoint
        app.MapGet("/health", () => new { status = "ok", timestamp = DateTimeOffset.UtcNow.ToString("O") })
            .WithName("Health")
            .WithTags("Health");

        // Agent chat endpoint - POST /v1/agent
        app.MapPost("/v1/agent", async (
            [FromBody] ChatRequest request,
            AgentLoop agentLoop,
            ISessionManager sessionManager,
            ClawSharpConfig config,
            ILogger<AgentLoop> agentLogger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest(new { error = "Message is required" });
            }

            // Get or create session
            var sessionKey = request.SessionKey ?? $"web-{Guid.NewGuid():N}";
            var session = await sessionManager.GetOrCreateAsync(
                sessionKey,
                "web",
                sessionKey,
                ct);

            // Add user message to history
            session.History.Add(new LlmMessage("user", request.Message));

            // Run agent loop
            var agentRequest = new AgentLoop.AgentRequest(
                request.Model ?? config.DefaultModel ?? "default",
                session.History
            );

            var result = await agentLoop.RunAsync(agentRequest, ct);

            // Add assistant response to history
            session.History.Add(new LlmMessage("assistant", result.Content));
            await sessionManager.SaveAsync(session, ct);

            // Map tool executions
            var toolInfos = result.ToolExecutions.Select(t => new ToolExecutionInfo(
                t.ToolCallId,
                t.ToolName,
                t.ArgumentsJson,
                t.Result.Success,
                t.Result.Output,
                t.Result.Error
            )).ToList();

            return Results.Ok(new ChatResponse(
                result.Content,
                sessionKey,
                toolInfos
            ));
        })
            .WithName("Chat")
            .WithTags("Agent");

        // Sessions endpoint - GET /v1/sessions
        app.MapGet("/v1/sessions", async (
            ISessionManager sessionManager,
            CancellationToken ct) =>
        {
            var sessions = await sessionManager.ListAsync(ct);
            return Results.Ok(sessions.Select(s => new
            {
                s.SessionKey,
                s.Channel,
                s.ChatId,
                s.Summary,
                s.Created,
                s.LastActive,
                MessageCount = s.History.Count
            }));
        })
            .WithName("Sessions")
            .WithTags("Sessions");

        // Messages endpoint - POST /v1/messages
        app.MapPost("/v1/messages", async (
            [FromBody] MessageRequest request,
            IMessageBus messageBus,
            ClawSharpConfig config,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Results.BadRequest(new { error = "Content is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Channel))
            {
                return Results.BadRequest(new { error = "Channel is required" });
            }

            if (string.IsNullOrWhiteSpace(request.ChatId))
            {
                return Results.BadRequest(new { error = "ChatId is required" });
            }

            // Publish message to the message bus
            var channelMessage = new ChannelMessage(
                Guid.NewGuid().ToString(),
                "web",
                request.Content,
                request.Channel,
                request.ChatId,
                DateTimeOffset.UtcNow
            );

            await messageBus.PublishAsync(channelMessage, ct);

            return Results.Accepted();
        })
            .WithName("Messages")
            .WithTags("Messages");
    }
}
