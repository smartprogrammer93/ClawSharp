using Microsoft.Extensions.Logging;

namespace ClawSharp.Providers;

/// <summary>
/// OpenRouter provider - OpenAI-compatible with extra required headers.
/// </summary>
public class OpenRouterProvider : CompatibleProvider
{
    private const string DefaultBaseUrl = "https://openrouter.ai/api/v1/";

    public OpenRouterProvider(HttpClient httpClient, ILogger<OpenRouterProvider> logger)
        : this(httpClient, logger, DefaultBaseUrl)
    {
    }

    public OpenRouterProvider(HttpClient httpClient, ILogger<OpenRouterProvider> logger, string baseUrl)
        : base("openrouter", httpClient, logger)
    {
        // Set default base URL if not already set
        if (httpClient.BaseAddress == null)
        {
            httpClient.BaseAddress = new Uri(baseUrl);
        }
        
        // Add required OpenRouter headers
        httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/clawsharp");
        httpClient.DefaultRequestHeaders.Add("X-Title", "ClawSharp");
    }
}
