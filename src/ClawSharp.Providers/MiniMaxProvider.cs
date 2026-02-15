using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ClawSharp.Core.Providers;
using Microsoft.Extensions.Logging;

namespace ClawSharp.Providers;

/// <summary>
/// MiniMax provider implementation using Anthropic-compatible API.
/// API documentation: https://www.minimaxi.com/document/Guides/Model-Text/Text-Generation.md
/// </summary>
public class MiniMaxProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<MiniMaxProvider> _logger;
    private readonly string _groupId;

    public string Name => "minimax";

    public MiniMaxProvider(HttpClient httpClient, ILogger<MiniMaxProvider> logger, string groupId)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _groupId = groupId ?? throw new ArgumentNullException(nameof(groupId));
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("", ct);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MiniMax API not available");
            return false;
        }
    }

    public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
    {
        // MiniMax doesn't have a public models list endpoint via this API
        // Supported models: MiniMax-M2.1, MiniMax-M2.5
        return Task.FromResult<IReadOnlyList<string>>(["MiniMax-M2.1", "MiniMax-M2.5"]);
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        var body = BuildRequestBody(request);
        var response = await _http.PostAsJsonAsync("v1/text/chatcompletion_v2", body, ct);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return ParseResponse(json);
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request, 
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = BuildRequestBody(request, stream: true);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/text/chatcompletion_v2")
        {
            Content = JsonContent.Create(body)
        };

        using var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
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

    private object BuildRequestBody(LlmRequest request, bool stream = false)
    {
        var messages = new List<JsonElement>();
        string? systemPrompt = null;
        
        foreach (var msg in request.Messages)
        {
            if (msg.Role == "system")
            {
                // Store system prompt - MiniMax uses separate field
                systemPrompt = msg.Content;
            }
            else if (msg.Role == "tool")
            {
                // Tool results are sent as user messages with tool role
                var toolResultObj = new
                {
                    role = "user",
                    content = msg.Content
                };
                messages.Add(JsonSerializer.SerializeToElement(toolResultObj));
                
                // Also add assistant message to continue the conversation
                var assistantContinue = new
                {
                    role = "assistant",
                    content = ""
                };
                messages.Add(JsonSerializer.SerializeToElement(assistantContinue));
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
                    tool_calls = toolUseArray
                };
                messages.Add(JsonSerializer.SerializeToElement(assistantMsg));
            }
            else
            {
                // Regular message
                var role = msg.Role == "assistant" ? "assistant" : msg.Role;
                var regularMsg = new { role = role, content = msg.Content };
                messages.Add(JsonSerializer.SerializeToElement(regularMsg));
            }
        }

        var body = new Dictionary<string, object>
        {
            ["model"] = request.Model,
            ["messages"] = messages.Select(m => JsonSerializer.Deserialize<object>(m.ToString())).ToArray(),
            ["stream"] = stream
        };

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            body["system_prompt"] = systemPrompt;
        }

        if (request.MaxTokens.HasValue)
        {
            body["max_tokens"] = request.MaxTokens.Value;
        }

        if (request.Tools != null && request.Tools.Count > 0)
        {
            body["tools"] = request.Tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = t.ParametersSchema
                }
            }).ToArray();
        }

        return body;
    }

    private LlmResponse ParseResponse(JsonElement json)
    {
        var content = "";
        var toolCalls = new List<ToolCallRequest>();
        
        if (json.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            
            if (firstChoice.TryGetProperty("message", out var message))
            {
                // Handle content as string
                if (message.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
                {
                    content = contentProp.GetString() ?? "";
                }
                // Handle content as array (MixedChat)
                else if (message.TryGetProperty("content", out var contentArray) && contentArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var block in contentArray.EnumerateArray())
                    {
                        if (block.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "text")
                        {
                            if (block.TryGetProperty("text", out var textProp))
                            {
                                content += textProp.GetString();
                            }
                        }
                        else if (block.TryGetProperty("type", out var toolTypeProp) && toolTypeProp.GetString() == "tool_call")
                        {
                            var id = block.GetProperty("id").GetString()!;
                            var name = block.GetProperty("name").GetString()!;
                            var argumentsInput = block.GetProperty("input");
                            var arguments = JsonSerializer.Serialize(argumentsInput);
                            
                            toolCalls.Add(new ToolCallRequest(id, name, arguments));
                        }
                    }
                }
            }
            
            if (firstChoice.TryGetProperty("finish_reason", out var finishReasonProp))
            {
                // Parse finish reason
            }
        }
        
        UsageInfo? usage = null;
        if (json.TryGetProperty("usage", out var usageProp))
        {
            var promptTokens = usageProp.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
            var completionTokens = usageProp.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
            usage = new UsageInfo(promptTokens, completionTokens, promptTokens + completionTokens);
        }
        
        var finishReason = "stop";
        if (json.TryGetProperty("choices", out var c) && c.GetArrayLength() > 0 && c[0].TryGetProperty("finish_reason", out var fr))
        {
            finishReason = fr.GetString() ?? "stop";
        }
        
        return new LlmResponse(content, toolCalls, finishReason, usage);
    }

    private LlmStreamChunk? ParseStreamChunk(JsonElement json)
    {
        if (!json.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            return null;

        var choice = choices[0];
        var delta = choice.GetProperty("delta");

        string? contentDelta = null;
        
        // Handle content as string
        if (delta.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
        {
            contentDelta = contentProp.GetString();
        }
        // Handle content as array
        else if (delta.TryGetProperty("content", out var contentArray) && contentArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in contentArray.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "text")
                {
                    if (block.TryGetProperty("text", out var textProp))
                    {
                        contentDelta = (contentDelta ?? "") + textProp.GetString();
                    }
                }
            }
        }

        var finishReason = choice.TryGetProperty("finish_reason", out var fr) && fr.ValueKind != JsonValueKind.Null
            ? fr.GetString()
            : null;
        
        return new LlmStreamChunk(contentDelta, null, finishReason, null);
    }
}
