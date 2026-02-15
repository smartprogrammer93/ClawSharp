using ClawSharp.Agent;
using ClawSharp.Core.Channels;
using ClawSharp.Core.Config;
using ClawSharp.Core.Providers;
using ClawSharp.Core.Sessions;
using ClawSharp.Core.Memory;
using ClawSharp.Infrastructure.Messaging;
using ClawSharp.Memory;
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
/// Request model for adding memory.
/// </summary>
public record AddMemoryRequest(string Key, string Content, string Category = "Core");

/// <summary>
/// Memory entry DTO.
/// </summary>
public record MemoryEntryDto(
    string Id,
    string Key,
    string Content,
    string Category,
    string Timestamp,
    string? SessionId = null,
    double? Score = null)
{
    public MemoryEntryDto(MemoryEntry entry) : this(
        entry.Id,
        entry.Key,
        entry.Content,
        entry.Category.ToString(),
        entry.Timestamp.ToString("O"),
        entry.SessionId,
        entry.Score)
    { }
}

/// <summary>
/// Config DTO.
/// </summary>
public record ConfigDto(
    string WorkspaceDir,
    string DataDir,
    string? DefaultProvider,
    string? DefaultModel,
    double DefaultTemperature,
    int MaxContextTokens,
    ProvidersConfigDto? Providers = null,
    GatewayConfigDto? Gateway = null,
    ChannelsConfigDto? Channels = null)
{
    public ConfigDto(ClawSharpConfig config) : this(
        config.WorkspaceDir,
        config.DataDir,
        config.DefaultProvider,
        config.DefaultModel,
        config.DefaultTemperature,
        config.MaxContextTokens,
        new ProvidersConfigDto(config.Providers),
        new GatewayConfigDto(config.Gateway),
        new ChannelsConfigDto(config.Channels))
    { }
}

public record ProvidersConfigDto(
    string? Openai_ApiKey,
    string? Openai_BaseUrl,
    string? Anthropic_ApiKey,
    string? Anthropic_BaseUrl,
    string? OpenRouter_ApiKey,
    string? OpenRouter_BaseUrl,
    string? Ollama_BaseUrl,
    string? MiniMax_ApiKey,
    string? MiniMax_BaseUrl)
{
    public ProvidersConfigDto(ProvidersConfig config) : this(
        config.Openai?.ApiKey,
        config.Openai?.BaseUrl,
        config.Anthropic?.ApiKey,
        config.Anthropic?.BaseUrl,
        config.OpenRouter?.ApiKey,
        config.OpenRouter?.BaseUrl,
        config.Ollama?.BaseUrl,
        config.MiniMax?.ApiKey,
        config.MiniMax?.BaseUrl)
    { }
}

public record GatewayConfigDto(int Port, string? Host, bool EnableUi)
{
    public GatewayConfigDto(GatewayConfig config) : this(
        config.Port,
        config.Host,
        config.EnableUi)
    { }
}

public record ChannelsConfigDto(
    bool Telegram_Enabled,
    string? Telegram_BotToken,
    bool Discord_Enabled,
    string? Discord_BotToken,
    bool Slack_Enabled,
    string? Slack_AppToken)
{
    public ChannelsConfigDto(ChannelsConfig config) : this(
        config.Telegram != null,
        config.Telegram?.BotToken,
        config.Discord != null,
        config.Discord?.BotToken,
        config.Slack != null,
        config.Slack?.AppToken)
    { }
}

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

        // Status endpoint - GET /status
        app.MapGet("/status", async (
            ISessionManager sessionManager,
            ClawSharpConfig config,
            CancellationToken ct) =>
        {
            var sessions = await sessionManager.ListAsync(ct);
            return Results.Ok(new
            {
                status = "ok",
                timestamp = DateTimeOffset.UtcNow.ToString("O"),
                channels = Array.Empty<object>(),
                memoryCount = 0,
                sessionCount = sessions.Count
            });
        })
            .WithName("Status")
            .WithTags("Status");

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

        // Memory endpoints - GET /v1/memory
        app.MapGet("/v1/memory", async (
            [FromQuery] string? query = null,
            [FromQuery] int limit = 50,
            CancellationToken ct = default) =>
        {
            var memoryStore = app.Services.GetRequiredService<IMemoryStore>();
            
            if (!string.IsNullOrWhiteSpace(query))
            {
                var results = await memoryStore.SearchAsync(query, limit, ct);
                return Results.Ok(results.Select(m => new MemoryEntryDto(m)));
            }
            
            var memories = await memoryStore.ListAsync(limit: limit, ct: ct);
            return Results.Ok(memories.Select(m => new MemoryEntryDto(m)));
        })
            .WithName("GetMemory")
            .WithTags("Memory");

        // Memory endpoints - POST /v1/memory
        app.MapPost("/v1/memory", async (
            [FromBody] AddMemoryRequest request,
            CancellationToken ct = default) =>
        {
            if (string.IsNullOrWhiteSpace(request.Key))
            {
                return Results.BadRequest(new { error = "Key is required" });
            }
            
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Results.BadRequest(new { error = "Content is required" });
            }

            var memoryStore = app.Services.GetRequiredService<IMemoryStore>();
            var category = Enum.TryParse<MemoryCategory>(request.Category, true, out var cat) 
                ? cat 
                : MemoryCategory.Core;
            
            await memoryStore.StoreAsync(request.Key, request.Content, category, ct);
            var entry = await memoryStore.GetAsync(request.Key, ct);
            
            return Results.Created($"/v1/memory/{request.Key}", new MemoryEntryDto(entry!));
        })
            .WithName("AddMemory")
            .WithTags("Memory");

        // Memory endpoints - DELETE /v1/memory/{key}
        app.MapDelete("/v1/memory/{key}", async (
            string key,
            CancellationToken ct = default) =>
        {
            var memoryStore = app.Services.GetRequiredService<IMemoryStore>();
            await memoryStore.DeleteAsync(key, ct);
            return Results.NoContent();
        })
            .WithName("DeleteMemory")
            .WithTags("Memory");

        // Config endpoint - GET /v1/config
        app.MapGet("/v1/config", (
            ClawSharpConfig config) =>
        {
            return Results.Ok(new ConfigDto(config));
        })
            .WithName("GetConfig")
            .WithTags("Config");

        // Config endpoint - PUT /v1/config
        app.MapPut("/v1/config", (
            [FromBody] ConfigDto configDto,
            ClawSharpConfig config,
            HttpContext httpContext) =>
        {
            // Update config values (in-memory for now)
            if (configDto.DefaultProvider != null)
                config.DefaultProvider = configDto.DefaultProvider;
            if (configDto.DefaultModel != null)
                config.DefaultModel = configDto.DefaultModel;
            config.DefaultTemperature = configDto.DefaultTemperature > 0 ? configDto.DefaultTemperature : config.DefaultTemperature;
            config.MaxContextTokens = configDto.MaxContextTokens > 0 ? configDto.MaxContextTokens : config.MaxContextTokens;
            
            return Results.Ok(new { status = "updated" });
        })
            .WithName("UpdateConfig")
            .WithTags("Config");

        // ===== API-prefixed endpoints for Blazor UI =====

        // Health check endpoint - GET /api/health
        app.MapGet("/api/health", () => new { status = "ok", timestamp = DateTimeOffset.UtcNow.ToString("O") })
            .WithName("ApiHealth")
            .WithTags("Health");

        // Status endpoint - GET /api/status
        app.MapGet("/api/status", async (
            ISessionManager sessionManager,
            ClawSharpConfig config,
            IReadOnlyList<ClawSharp.Core.Channels.IChannel> channels,
            CancellationToken ct) =>
        {
            var sessions = await sessionManager.ListAsync(ct);
            var providers = app.Services.GetServices<ILlmProvider>().ToList();
            
            var status = new
            {
                version = "1.0.0",
                startTime = DateTime.UtcNow,
                uptime = (DateTime.UtcNow - DateTime.UtcNow).ToString(),
                providers = providers.Select(p => new
                {
                    name = p.Name,
                    isAvailable = p.IsAvailableAsync(ct).Result,
                    models = p.ListModelsAsync(ct).Result
                }),
                channels = channels.Select(c => new
                {
                    name = c.Name,
                    isRunning = true // ChannelManager would set this
                }),
                memory = new
                {
                    totalEntries = 0,
                    totalSizeBytes = 0L
                }
            };
            
            return Results.Ok(status);
        })
            .WithName("ApiStatus")
            .WithTags("Status");

        // Sessions endpoint - GET /api/sessions
        app.MapGet("/api/sessions", async (
            ISessionManager sessionManager,
            CancellationToken ct) =>
        {
            var sessions = await sessionManager.ListAsync(ct);
            return Results.Ok(sessions.Select(s => new
            {
                sessionKey = s.SessionKey,
                channel = s.Channel,
                chatId = s.ChatId,
                messageCount = s.History.Count,
                createdAt = s.Created
            }));
        })
            .WithName("ApiSessions")
            .WithTags("Sessions");

        // Session delete endpoint - DELETE /api/sessions/{key}
        app.MapDelete("/api/sessions/{key}", async (
            string key,
            ISessionManager sessionManager,
            CancellationToken ct) =>
        {
            await sessionManager.DeleteAsync(key, ct);
            return Results.NoContent();
        })
            .WithName("ApiDeleteSession")
            .WithTags("Sessions");

        // Memory endpoints - GET /api/memory
        app.MapGet("/api/memory", async (
            [FromQuery] string? query = null,
            [FromQuery] int limit = 50,
            CancellationToken ct = default) =>
        {
            var memoryStore = app.Services.GetRequiredService<IMemoryStore>();
            
            List<MemoryEntryDto> memories;
            if (!string.IsNullOrWhiteSpace(query))
            {
                var results = await memoryStore.SearchAsync(query, limit, ct);
                memories = results.Select(m => new MemoryEntryDto(m)).ToList();
            }
            else
            {
                var allMemories = await memoryStore.ListAsync(limit: limit, ct: ct);
                memories = allMemories.Select(m => new MemoryEntryDto(m)).ToList();
            }
            
            return Results.Ok(memories.Select(m => new
            {
                key = m.Key,
                content = m.Content,
                createdAt = m.Timestamp
            }));
        })
            .WithName("ApiGetMemory")
            .WithTags("Memory");

        // Memory add endpoint - POST /api/memory
        app.MapPost("/api/memory", async (
            [FromBody] AddMemoryRequest request,
            CancellationToken ct = default) =>
        {
            if (string.IsNullOrWhiteSpace(request.Key))
            {
                return Results.BadRequest(new { error = "Key is required" });
            }
            
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Results.BadRequest(new { error = "Content is required" });
            }

            var memoryStore = app.Services.GetRequiredService<IMemoryStore>();
            var category = Enum.TryParse<MemoryCategory>(request.Category, true, out var cat) 
                ? cat 
                : MemoryCategory.Core;
            
            await memoryStore.StoreAsync(request.Key, request.Content, category, ct);
            
            return Results.Ok(new { status = "added" });
        })
            .WithName("ApiAddMemory")
            .WithTags("Memory");

        // Memory delete endpoint - DELETE /api/memory/{key}
        app.MapDelete("/api/memory/{key}", async (
            string key,
            CancellationToken ct = default) =>
        {
            var memoryStore = app.Services.GetRequiredService<IMemoryStore>();
            await memoryStore.DeleteAsync(key, ct);
            return Results.NoContent();
        })
            .WithName("ApiDeleteMemory")
            .WithTags("Memory");

        // Settings endpoint - GET /api/settings
        app.MapGet("/api/settings", (
            ClawSharpConfig config) =>
        {
            var providers = app.Services.GetServices<ILlmProvider>().ToList();
            var channels = app.Services.GetService<IReadOnlyList<ClawSharp.Core.Channels.IChannel>>();
            
            return Results.Ok(new
            {
                dataDir = config.DataDir,
                workspaceDir = config.WorkspaceDir,
                defaultProvider = config.DefaultProvider,
                defaultModel = config.DefaultModel,
                availableProviders = providers.Select(p => p.Name).ToList(),
                availableModels = new List<string> { "default" }, // Would come from provider
                apiKeys = new List<object>(), // Don't expose actual keys
                channels = (channels ?? []).Select(c => new
                {
                    name = c.Name,
                    isEnabled = true,
                    isRunning = true
                }).ToList()
            });
        })
            .WithName("ApiSettings")
            .WithTags("Settings");
    }
}
