using System.Net.Http.Json;

namespace ClawSharp.UI.Services;

/// <summary>
/// Service for interacting with the ClawSharp API.
/// </summary>
public class ApiService
{
    private readonly HttpClient _http;

    public ApiService(HttpClient http)
    {
        _http = http;
    }

    // Chat endpoints
    public async Task<ChatResponse> SendMessageAsync(ChatRequest request)
    {
        var response = await _http.PostAsJsonAsync("/v1/agent", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatResponse>() 
            ?? throw new InvalidOperationException("Failed to parse response");
    }

    // Memory endpoints
    public async Task<List<MemoryEntry>> SearchMemoryAsync(string query, int limit = 10)
    {
        var response = await _http.GetAsync($"/v1/memory?query={Uri.EscapeDataString(query)}&limit={limit}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<MemoryEntry>>() ?? new List<MemoryEntry>();
    }

    public async Task<List<MemoryEntry>> ListMemoryAsync(int limit = 50)
    {
        var response = await _http.GetAsync($"/v1/memory?limit={limit}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<MemoryEntry>>() ?? new List<MemoryEntry>();
    }

    public async Task<MemoryEntry> AddMemoryAsync(string key, string content, string category = "Core")
    {
        var response = await _http.PostAsJsonAsync("/v1/memory", new { key, content, category });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MemoryEntry>() 
            ?? throw new InvalidOperationException("Failed to parse response");
    }

    public async Task DeleteMemoryAsync(string key)
    {
        var response = await _http.DeleteAsync($"/v1/memory/{Uri.EscapeDataString(key)}");
        response.EnsureSuccessStatusCode();
    }

    // Settings endpoints
    public async Task<ConfigResponse> GetConfigAsync()
    {
        var response = await _http.GetAsync("/v1/config");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ConfigResponse>() 
            ?? throw new InvalidOperationException("Failed to parse response");
    }

    public async Task UpdateConfigAsync(ConfigResponse config)
    {
        var response = await _http.PutAsJsonAsync("/v1/config", config);
        response.EnsureSuccessStatusCode();
    }

    // Sessions endpoints
    public async Task<List<SessionInfo>> ListSessionsAsync()
    {
        var response = await _http.GetAsync("/v1/sessions");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<SessionInfo>>() ?? new List<SessionInfo>();
    }

    public async Task DeleteSessionAsync(string sessionKey)
    {
        var response = await _http.DeleteAsync($"/v1/sessions/{Uri.EscapeDataString(sessionKey)}");
        response.EnsureSuccessStatusCode();
    }

    // Status endpoint
    public async Task<StatusResponse> GetStatusAsync()
    {
        var response = await _http.GetAsync("/status");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StatusResponse>() 
            ?? throw new InvalidOperationException("Failed to parse response");
    }
}

// Request/Response DTOs
public record ChatRequest(string Message, string? SessionKey = null, string? Model = null);
public record ChatResponse(string Response, string? SessionKey, List<ToolExecutionInfo>? ToolExecutions = null);
public record ToolExecutionInfo(string ToolCallId, string ToolName, string ArgumentsJson, bool Success, string? Output, string? Error);

public record MemoryEntry(string Id, string Key, string Content, string Category, DateTimeOffset Timestamp, string? SessionId = null, double? Score = null);
public record ConfigResponse(
    string WorkspaceDir,
    string DataDir,
    string? DefaultProvider,
    string? DefaultModel,
    double DefaultTemperature,
    int MaxContextTokens,
    ProvidersConfigResponse Providers,
    GatewayConfigResponse Gateway,
    ChannelsConfigResponse Channels
);
public record ProvidersConfigResponse(
    ProviderConfigResponse? Openai,
    ProviderConfigResponse? Anthropic,
    ProviderConfigResponse? OpenRouter,
    ProviderConfigResponse? Ollama,
    ProviderConfigResponse? MiniMax
);
public record ProviderConfigResponse(string? ApiKey, string? BaseUrl, string? DefaultModel);
public record GatewayConfigResponse(int Port, string? Host, bool EnableCors);
public record ChannelsConfigResponse(
    TelegramConfigResponse? Telegram,
    DiscordConfigResponse? Discord,
    SlackConfigResponse? Slack
);
public record TelegramConfigResponse(string? BotToken, List<string>? AllowedUsers);
public record DiscordConfigResponse(string? BotToken, List<string>? AllowedGuilds);
public record SlackConfigResponse(string? AppToken, string? BotToken);

public record SessionInfo(string SessionKey, string Channel, string ChatId, string? Summary, DateTimeOffset Created, DateTimeOffset LastActive, int MessageCount);

public record StatusResponse(
    string Status,
    DateTimeOffset Timestamp,
    List<ChannelStatusResponse> Channels,
    int MemoryCount,
    int SessionCount
);
public record ChannelStatusResponse(string Name, bool IsRunning, string? Error = null);
