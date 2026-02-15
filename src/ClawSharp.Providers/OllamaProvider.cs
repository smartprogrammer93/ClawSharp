using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ClawSharp.Providers;

/// <summary>
/// Ollama provider - local OpenAI-compatible LLM server.
/// </summary>
public class OllamaProvider : CompatibleProvider
{
    private const string DefaultBaseUrl = "http://localhost:11434/v1/";

    public OllamaProvider(HttpClient httpClient, ILogger<OllamaProvider> logger)
        : base("ollama", httpClient, logger)
    {
        // Set default base URL if not already set
        if (httpClient.BaseAddress == null)
        {
            httpClient.BaseAddress = new Uri(DefaultBaseUrl);
        }
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            // Ollama uses /api/tags for listing models
            var response = await _http.GetAsync("models", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ollama API not available");
            return false;
        }
    }

    public override async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
    {
        try
        {
            // Ollama uses /api/tags endpoint
            var response = await _http.GetAsync("models", ct);
            if (!response.IsSuccessStatusCode)
                return [];

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

            if (!json.TryGetProperty("models", out var modelsArray))
                return [];

            var models = new List<string>();
            foreach (var model in modelsArray.EnumerateArray())
            {
                if (model.TryGetProperty("name", out var name))
                {
                    // Ollama model names might include tags like "llama2:latest"
                    var nameStr = name.GetString();
                    if (!string.IsNullOrEmpty(nameStr))
                    {
                        // Just take the base name without tags
                        var baseName = nameStr.Split(':')[0];
                        if (!models.Contains(baseName))
                            models.Add(baseName);
                    }
                }
            }

            models.Sort();
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to list Ollama models");
            return [];
        }
    }
}
