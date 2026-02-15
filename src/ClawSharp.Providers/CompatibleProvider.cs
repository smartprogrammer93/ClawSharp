using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ClawSharp.Core.Providers;
using Microsoft.Extensions.Logging;

namespace ClawSharp.Providers;

/// <summary>
/// Base class for OpenAI-compatible providers (OpenRouter, Ollama, etc.)
/// </summary>
public class CompatibleProvider : ILlmProvider
{
    protected readonly HttpClient _http;
    protected readonly ILogger _logger;
    protected readonly string ProviderName;

    public string Name => ProviderName;

    public CompatibleProvider(string name, HttpClient httpClient, ILogger logger)
    {
        ProviderName = name ?? throw new ArgumentNullException(nameof(name));
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public virtual async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("models", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Provider} API not available", ProviderName);
            return false;
        }
    }

    public virtual async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("models", ct);
            if (!response.IsSuccessStatusCode)
                return [];

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            
            if (!json.TryGetProperty("data", out var data))
                return [];

            var models = new List<string>();
            foreach (var item in data.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var id))
                {
                    models.Add(id.GetString()!);
                }
            }

            models.Sort();
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to list models for {Provider}", ProviderName);
            return [];
        }
    }

    public virtual async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        var body = BuildRequestBody(request, stream: false);
        var response = await _http.PostAsJsonAsync("chat/completions", body, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return ParseResponse(json);
    }

    public virtual async IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = BuildRequestBody(request, stream: true);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
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

    protected virtual object BuildRequestBody(LlmRequest request, bool stream)
    {
        var body = new Dictionary<string, object>
        {
            ["model"] = request.Model,
            ["messages"] = request.Messages.Select(m => new
            {
                role = m.Role,
                content = m.Content,
                tool_calls = m.ToolCalls?.Select(tc => new
                {
                    id = tc.Id,
                    type = "function",
                    function = new
                    {
                        name = tc.Name,
                        arguments = tc.ArgumentsJson
                    }
                }).ToArray(),
                tool_call_id = m.ToolCallId,
                name = m.Name
            }).ToArray(),
            ["temperature"] = request.Temperature,
            ["stream"] = stream
        };

        if (request.MaxTokens.HasValue)
            body["max_tokens"] = request.MaxTokens.Value;

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

    protected virtual LlmResponse ParseResponse(JsonElement json)
    {
        var choice = json.GetProperty("choices")[0];
        var message = choice.GetProperty("message");

        var content = message.TryGetProperty("content", out var contentProp)
            ? contentProp.GetString() ?? ""
            : "";

        var toolCalls = new List<ToolCallRequest>();
        if (message.TryGetProperty("tool_calls", out var toolCallsArray))
        {
            foreach (var tc in toolCallsArray.EnumerateArray())
            {
                var id = tc.GetProperty("id").GetString()!;
                var function = tc.GetProperty("function");
                var name = function.GetProperty("name").GetString()!;
                var arguments = function.GetProperty("arguments").GetString()!;

                toolCalls.Add(new ToolCallRequest(id, name, arguments));
            }
        }

        var finishReason = choice.GetProperty("finish_reason").GetString()!;

        UsageInfo? usage = null;
        if (json.TryGetProperty("usage", out var usageProp))
        {
            usage = new UsageInfo(
                usageProp.GetProperty("prompt_tokens").GetInt32(),
                usageProp.GetProperty("completion_tokens").GetInt32(),
                usageProp.GetProperty("total_tokens").GetInt32()
            );
        }

        return new LlmResponse(content, toolCalls, finishReason, usage);
    }

    protected virtual LlmStreamChunk? ParseStreamChunk(JsonElement json)
    {
        if (!json.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            return null;

        var choice = choices[0];
        var delta = choice.GetProperty("delta");

        var contentDelta = delta.TryGetProperty("content", out var content)
            ? content.GetString()
            : null;

        var finishReason = choice.TryGetProperty("finish_reason", out var fr) && fr.ValueKind != JsonValueKind.Null
            ? fr.GetString()
            : null;

        return new LlmStreamChunk(contentDelta, null, finishReason, null);
    }
}
