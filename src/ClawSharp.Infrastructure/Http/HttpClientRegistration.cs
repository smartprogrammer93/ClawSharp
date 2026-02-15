using ClawSharp.Core.Auth;
using ClawSharp.Core.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClawSharp.Infrastructure.Http;

/// <summary>
/// Extension methods for registering HTTP clients for LLM providers.
/// </summary>
public static class HttpClientRegistration
{
    /// <summary>
    /// Registers named HTTP clients for each LLM provider with retry policies.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProviderHttpClients(this IServiceCollection services)
    {
        // OpenAI client
        services.AddHttpClient("openai", (sp, client) =>
        {
            var config = sp.GetRequiredService<ClawSharpConfig>();
            client.BaseAddress = new Uri(config.Providers.Openai?.BaseUrl ?? "https://api.openai.com/v1");
            if (config.Providers.Openai?.ApiKey is { } key)
                client.DefaultRequestHeaders.Authorization = new("Bearer", key);
            client.DefaultRequestHeaders.Add("User-Agent", $"ClawSharp/{GetVersion()}");
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        // Anthropic client - supports both api_key and oauth modes
        services.AddHttpClient("anthropic", (sp, client) =>
        {
            var config = sp.GetRequiredService<ClawSharpConfig>();
            var providerConfig = config.Providers.Anthropic;
            var authMode = providerConfig?.AuthMode?.ToLowerInvariant() ?? "api_key";
            
            client.BaseAddress = new Uri(providerConfig?.BaseUrl ?? "https://api.anthropic.com");
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            client.Timeout = TimeSpan.FromSeconds(120);
            
            if (authMode == "oauth")
            {
                // OAuth mode - the OAuthTokenHandler will add the token
                // Register the token manager for later use
                var profileId = providerConfig?.AuthProfileId ?? "anthropic:default";
                var profilesPath = providerConfig?.AuthProfilesPath 
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                        ".openclaw", "agents", "main", "agent", "auth-profiles.json");
                
                var logger = sp.GetService<ILogger<OAuthTokenManager>>();
                var tokenManager = new OAuthTokenManager(profilesPath, profileId, logger);
                
                // Store the token manager in the service provider for the provider to use
                services.AddSingleton(tokenManager);
            }
            else
            {
                // API key mode - use the static API key
                if (providerConfig?.ApiKey is { } key)
                    client.DefaultRequestHeaders.Add("x-api-key", key);
            }
        });

        // OpenRouter client
        services.AddHttpClient("openrouter", (sp, client) =>
        {
            var config = sp.GetRequiredService<ClawSharpConfig>();
            client.BaseAddress = new Uri(config.Providers.OpenRouter?.BaseUrl ?? "https://openrouter.ai/api/v1");
            if (config.Providers.OpenRouter?.ApiKey is { } key)
                client.DefaultRequestHeaders.Authorization = new("Bearer", key);
            client.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/clawsharp");
            client.DefaultRequestHeaders.Add("X-Title", "ClawSharp");
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        // Ollama client
        services.AddHttpClient("ollama", (sp, client) =>
        {
            var config = sp.GetRequiredService<ClawSharpConfig>();
            client.BaseAddress = new Uri(config.Providers.Ollama?.BaseUrl ?? "http://localhost:11434/v1");
            if (config.Providers.Ollama?.ApiKey is { } key)
                client.DefaultRequestHeaders.Authorization = new("Bearer", key);
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        // MiniMax client - uses Anthropic-compatible API at api.minimax.io
        services.AddHttpClient("minimax", (sp, client) =>
        {
            var config = sp.GetRequiredService<ClawSharpConfig>();
            var providerConfig = config.Providers.MiniMax;
            var baseUrl = providerConfig?.BaseUrl ?? "https://api.minimax.io/v1";
            var apiKey = providerConfig?.ApiKey ?? "";
            var groupId = providerConfig?.GroupId ?? "";
            
            client.BaseAddress = new Uri(baseUrl);
            if (!string.IsNullOrEmpty(apiKey))
                client.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
            if (!string.IsNullOrEmpty(groupId))
                client.DefaultRequestHeaders.Add("X-Group-Id", groupId);
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        return services;
    }

    private static string GetVersion()
    {
        return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    }
}
