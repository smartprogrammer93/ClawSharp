using Microsoft.Extensions.Logging;

namespace ClawSharp.Infrastructure.Tunnels;

/// <summary>
/// Factory for creating tunnel instances by provider name.
/// </summary>
public sealed class TunnelFactory
{
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>List of supported tunnel providers.</summary>
    public static IReadOnlyList<string> SupportedProviders { get; } = ["cloudflare", "ngrok", "tailscale"];

    public TunnelFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Create a tunnel instance for the given provider.
    /// </summary>
    /// <param name="provider">Provider name (case-insensitive).</param>
    /// <param name="token">Optional auth token.</param>
    /// <param name="domain">Optional domain/hostname.</param>
    public ITunnel Create(string provider, string? token = null, string? domain = null)
    {
        return provider.ToLowerInvariant() switch
        {
            "cloudflare" => new CloudflareTunnel(
                _loggerFactory.CreateLogger<CloudflareTunnel>(), token, domain),
            "ngrok" => new NgrokTunnel(
                _loggerFactory.CreateLogger<NgrokTunnel>(), token),
            "tailscale" => new TailscaleTunnel(
                _loggerFactory.CreateLogger<TailscaleTunnel>(), domain),
            _ => throw new ArgumentException(
                $"Unknown tunnel provider: '{provider}'. Supported: {string.Join(", ", SupportedProviders)}")
        };
    }
}
