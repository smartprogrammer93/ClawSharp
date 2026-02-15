using ClawSharp.Agent;
using ClawSharp.Core.Config;
using ClawSharp.Core.Memory;
using ClawSharp.Core.Providers;
using ClawSharp.Core.Sessions;
using ClawSharp.Core.Tools;
using ClawSharp.Infrastructure.Http;
using ClawSharp.Memory;
using ClawSharp.Providers;
using ClawSharp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClawSharp.Infrastructure.Providers;

/// <summary>
/// Extension methods for registering LLM providers with the DI container.
/// </summary>
public static class ProviderRegistration
{
    /// <summary>
    /// Registers all LLM providers and related services.
    /// </summary>
    public static IServiceCollection AddLlmProviders(this IServiceCollection services)
    {
        // Register HTTP clients first
        services.AddProviderHttpClients();

        // Register individual providers
        services.AddSingleton<OpenAiProvider>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<OpenAiProvider>>();
            return new OpenAiProvider(factory.CreateClient("openai"), logger);
        });

        services.AddSingleton<AnthropicProvider>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<AnthropicProvider>>();
            return new AnthropicProvider(factory.CreateClient("anthropic"), logger);
        });

        services.AddSingleton<OpenRouterProvider>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<OpenRouterProvider>>();
            return new OpenRouterProvider(factory.CreateClient("openrouter"), logger);
        });

        services.AddSingleton<OllamaProvider>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<OllamaProvider>>();
            return new OllamaProvider(factory.CreateClient("ollama"), logger);
        });

        // Register a provider resolver that can get the correct provider by name
        services.AddSingleton<Func<string, ILlmProvider>>(sp => providerName =>
        {
            return providerName.ToLowerInvariant() switch
            {
                "openai" => sp.GetRequiredService<OpenAiProvider>(),
                "anthropic" => sp.GetRequiredService<AnthropicProvider>(),
                "openrouter" => sp.GetRequiredService<OpenRouterProvider>(),
                "ollama" => sp.GetRequiredService<OllamaProvider>(),
                _ => throw new InvalidOperationException($"Unknown provider: {providerName}")
            };
        });

        // Register default provider based on config
        services.AddSingleton<ILlmProvider>(sp =>
        {
            var config = sp.GetRequiredService<ClawSharpConfig>();
            var providerResolver = sp.GetRequiredService<Func<string, ILlmProvider>>();
            var providerName = config.DefaultProvider ?? "openai";
            return providerResolver(providerName);
        });

        return services;
    }

    /// <summary>
    /// Registers all tools with the tool registry.
    /// </summary>
    public static IServiceCollection AddTools(this IServiceCollection services)
    {
        // Tools are registered as part of the registry initialization
        services.AddSingleton<IToolRegistry>(sp =>
        {
            var registry = new ToolRegistry();
            var security = sp.GetRequiredService<Core.Security.ISecurityPolicy>();
            var httpFactory = sp.GetRequiredService<IHttpClientFactory>();

            // Register all standard tools
            registry.Register(new ShellTool(security));
            registry.Register(new FileReadTool(security));
            registry.Register(new FileWriteTool(security));
            registry.Register(new EditFileTool(security));
            
            // Web tools need config
            var config = sp.GetRequiredService<ClawSharpConfig>();
            var braveApiKey = Environment.GetEnvironmentVariable("BRAVE_API_KEY") ?? "";
            registry.Register(new WebSearchTool(httpFactory.CreateClient("websearch"), braveApiKey));
            registry.Register(new WebFetchTool(httpFactory.CreateClient("webfetch")));

            return registry;
        });

        return services;
    }

    /// <summary>
    /// Registers agent services (session manager, context builder, agent loop).
    /// </summary>
    public static IServiceCollection AddAgentServices(this IServiceCollection services)
    {
        // Session manager
        services.AddSingleton<ISessionManager>(sp =>
        {
            var config = sp.GetRequiredService<ClawSharpConfig>();
            var dbPath = Path.Combine(config.DataDir, "sessions.db");
            return new SqliteSessionManager(dbPath);
        });

        // Memory store
        services.AddSingleton<IMemoryStore>(sp =>
        {
            var config = sp.GetRequiredService<ClawSharpConfig>();
            var dbPath = Path.Combine(config.DataDir, config.Memory.DbPath ?? "memory.db");
            return new SqliteMemoryStore(dbPath);
        });

        // Context builder
        services.AddSingleton<ContextBuilder>();

        // Agent loop
        services.AddSingleton<AgentLoop>();

        return services;
    }
}
