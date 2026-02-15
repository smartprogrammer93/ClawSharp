using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ClawSharp.Core.Auth;
using ClawSharp.Core.Providers;
using Microsoft.Extensions.Logging;

namespace ClawSharp.Providers;

/// <summary>
/// Anthropic Messages API provider implementation.
/// </summary>
public class AnthropicProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<AnthropicProvider> _logger;
    private readonly OAuthTokenManager? _tokenManager;

    public string Name => "anthropic";

    /// <summary>
    /// Creates an AnthropicProvider with API key authentication.
    /// </summary>
    public AnthropicProvider(HttpClient httpClient, ILogger<AnthropicProvider> logger)
        : this(httpClient, logger, null)
    {
    }

    /// <summary>
    /// Creates an AnthropicProvider with OAuth authentication.
    /// </summary>
    public AnthropicProvider(HttpClient httpClient, ILogger<AnthropicProvider> logger, OAuthTokenManager? tokenManager)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenManager = tokenManager;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            // Anthropic doesn't have a dedicated health endpoint, so we just check if we can connect
            var response = await _http.GetAsync("", ct);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Anthropic API not available");
            return false;
        }
    }

    public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
    {
        // Anthropic doesn't have a public models list endpoint
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);
        
        var body = BuildRequestBody(request);
        var response = await _http.PostAsJsonAsync("v1/messages", body, ct);
        
        // Handle 401 - token may be expired
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && _tokenManager != null)
        {
            _logger.LogWarning("Received 401, attempting to refresh OAuth token");
            var refreshed = await _tokenManager.RefreshTokenAsync(_http, ct);
            if (refreshed)
            {
                await EnsureAuthenticatedAsync(ct);
                response.Dispose();
                response = await _http.PostAsJsonAsync("v1/messages", body, ct);
            }
        }
        
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return ParseResponse(json);
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request, 
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);
        
        var body = BuildRequestBody(request, stream: true);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = JsonContent.Create(body)
        };

        var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        
        // Handle 401 - token may be expired
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && _tokenManager != null)
        {
            _logger.LogWarning("Received 401 in stream, attempting to refresh OAuth token");
            var refreshed = await _tokenManager.RefreshTokenAsync(_http, ct);
            if (refreshed)
            {
                await EnsureAuthenticatedAsync(ct);
                // For streaming, we need to restart the request
                httpRequest.Dispose();
                httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
                {
                    Content = JsonContent.Create(body)
                };
                response.Dispose();
                response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            }
        }
        
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;
            
            var data = line.Substring(6).Trim();
            if (data == "[DONE]") break;

            var json = JsonSerializer.Deserialize<JsonElement>(data);
            var chunk = ParseStreamChunk(json);
            if (chunk != null) yield return chunk;
        }
    }

    /// <summary>
    /// Ensures the request has the proper authentication header.
    /// </summary>
    private async Task EnsureAuthenticatedAsync(CancellationToken ct)
    {
        if (_tokenManager != null)
        {
            var token = await _tokenManager.GetAccessTokenAsync(_http, ct);
            if (!string.IsNullOrEmpty(token))
            {
                // Remove existing x-api-key and use Authorization header for OAuth
                _http.DefaultRequestHeaders.Remove("x-api-key");
                _http.DefaultRequestHeaders.Remove("Authorization");
                _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                _logger.LogDebug("Using OAuth token for authentication");
            }
        }
    }

    private object BuildRequestBody(LlmRequest request, bool stream = false)
    {
        var messages = new List<JsonElement>();
        JsonElement? systemJson = null;
        var toolResults = new List<JsonElement>();
        
        foreach (var msg in request.Messages)
        {
            if (msg.Role == "system")
            {
                // Store system prompt to add as top-level field
                systemJson = JsonSerializer.SerializeToElement(new { type = "text", text = msg.Content });
            }
            else if (msg.Role == "tool")
            {
                // Tool results are sent as content blocks with type "tool_result"
                var toolResultObj = new
                {
                    type = "tool_result",
                    tool_use_id = msg.ToolCallId,
                    content = msg.Content
                };
                toolResults.Add(JsonSerializer.SerializeToElement(toolResultObj));
            }
            else if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                // Assistant message with tool calls
                var toolUseArray = msg.ToolCalls.Select(tc =>
                {
                    var inputElement = JsonSerializer.Deserialize<JsonElement>(tc.ArgumentsJson);
                    return JsonSerializer.SerializeToElement(new
                    {
                        type = "tool_use",
                        id = tc.Id,
                        name = tc.Name,
                        input = inputElement
                    });
                }).ToArray();
                
                var assistantMsg = new
                {
                    role = "assistant",
                    content = string.IsNullOrEmpty(msg.Content) ? null : msg.Content,
                    tool_use = toolUseArray
                };
                messages.Add(JsonSerializer.SerializeToElement(assistantMsg));
            }
            else
            {
                // Regular message
                var regularMsg = new { role = msg.Role, content = msg.Content };
                messages.Add(JsonSerializer.SerializeToElement(regularMsg));
            }
        }

        // If we have tool results, we need to add them after the last user message
        // by creating a special message with content array
        if (toolResults.Count > 0)
        {
            var combinedContent = new List<JsonElement>();
            
            // Find last user message and get its content, then append tool results
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                var msgJson = messages[i];
                if (msgJson.TryGetProperty("role", out var roleProp) && roleProp.GetString() == "user")
                {
                    // Get existing content if it's a text block
                    if (msgJson.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
                    {
                        var textContent = contentProp.GetString();
                        if (!string.IsNullOrEmpty(textContent))
                        {
                            combinedContent.Add(JsonSerializer.SerializeToElement(new { type = "text", text = textContent }));
                        }
                    }
                    
                    // Add tool results
                    combinedContent.AddRange(toolResults);
                    
                    // Replace the user message with content array including tool results
                    var newUserMsg = JsonSerializer.SerializeToElement(new
                    {
                        role = "user",
                        content = combinedContent
                    });
                    messages[i] = newUserMsg;
                    break;
                }
            }
        }

        var body = new Dictionary<string, object>
        {
            ["model"] = request.Model,
            ["messages"] = messages.Select(m => JsonSerializer.Deserialize<object>(m.ToString())).ToArray(),
            ["stream"] = stream,
            ["max_tokens"] = request.MaxTokens ?? 4096
        };

        if (systemJson.HasValue)
        {
            body["system"] = systemJson.Value.GetProperty("text").GetString() ?? "";
        }

        if (request.Tools != null && request.Tools.Count > 0)
        {
            body["tools"] = request.Tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                input_schema = t.ParametersSchema
            }).ToArray();
        }

        return body;
    }

    private LlmResponse ParseResponse(JsonElement json)
    {
        var content = "";
        var toolCalls = new List<ToolCallRequest>();
        
        if (json.TryGetProperty("content", out var contentArray))
        {
            foreach (var block in contentArray.EnumerateArray())
            {
                var type = block.GetProperty("type").GetString();
                if (type == "text")
                {
                    content += block.GetProperty("text").GetString();
                }
                else if (type == "tool_use")
                {
                    var id = block.GetProperty("id").GetString()!;
                    var name = block.GetProperty("name").GetString()!;
                    var input = block.GetProperty("input");
                    var arguments = JsonSerializer.Serialize(input);
                    
                    toolCalls.Add(new ToolCallRequest(id, name, arguments));
                }
            }
        }
        
        var stopReason = json.TryGetProperty("stop_reason", out var reason) 
            ? reason.GetString() ?? "stop" 
            : "stop";
        
        UsageInfo? usage = null;
        if (json.TryGetProperty("usage", out var usageProp))
        {
            usage = new UsageInfo(
                usageProp.GetProperty("input_tokens").GetInt32(),
                usageProp.GetProperty("output_tokens").GetInt32(),
                usageProp.GetProperty("input_tokens").GetInt32() + usageProp.GetProperty("output_tokens").GetInt32()
            );
        }
        
        return new LlmResponse(content, toolCalls, stopReason, usage);
    }

    private LlmStreamChunk? ParseStreamChunk(JsonElement json)
    {
        if (!json.TryGetProperty("content", out var contentArray) || contentArray.GetArrayLength() == 0)
            return null;

        string? contentDelta = null;
        string? finishReason = null;
        
        foreach (var block in contentArray.EnumerateArray())
        {
            var type = block.GetProperty("type").GetString();
            if (type == "text")
            {
                contentDelta = (contentDelta ?? "") + block.GetProperty("text").GetString();
            }
            else if (type == "content_block_stop")
            {
                // Streaming stopped
            }
        }
        
        if (json.TryGetProperty("stop_reason", out var reason) && reason.ValueKind != JsonValueKind.Null)
        {
            finishReason = reason.GetString();
        }
        
        return new LlmStreamChunk(contentDelta, null, finishReason, null);
    }
}
