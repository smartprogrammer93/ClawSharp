using ClawSharp.Core.Channels;
using ClawSharp.Core.Config;
using ClawSharp.Core.Security;
using ClawSharp.Infrastructure.Messaging;
using ClawSharp.Infrastructure.Providers;
using ClawSharp.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ClawSharp.Infrastructure;

/// <summary>
/// Extension methods for registering ClawSharp services with the DI container.
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Registers all core ClawSharp services into the DI container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">The ClawSharp configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddClawSharp(this IServiceCollection services, ClawSharpConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // Configuration
        services.AddSingleton(config);

        // Logging via Serilog
        var logPath = Path.Combine(config.DataDir, "logs", "clawsharp-.log");
        var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        services.AddLogging(builder => builder.AddSerilog(logger, dispose: true));

        // Core services
        services.AddSingleton<IMessageBus, InProcessMessageBus>();
        services.AddSingleton<ISecurityPolicy, DefaultSecurityPolicy>();

        // LLM Providers
        services.AddLlmProviders();

        // Tools
        services.AddTools();

        // Agent services (session manager, context builder, agent loop)
        services.AddAgentServices();

        return services;
    }
}
