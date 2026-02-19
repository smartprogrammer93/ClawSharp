namespace ClawSharp.Infrastructure.Tunnels;

/// <summary>
/// Interface for tunnel providers that expose a local port publicly.
/// </summary>
public interface ITunnel
{
    /// <summary>Provider name (e.g., "cloudflare", "ngrok", "tailscale").</summary>
    string Name { get; }

    /// <summary>The public URL after the tunnel is started, or null if not running.</summary>
    string? PublicUrl { get; }

    /// <summary>Whether the tunnel process is currently running.</summary>
    bool IsRunning { get; }

    /// <summary>Start the tunnel for the given local port.</summary>
    Task StartAsync(int localPort, CancellationToken ct = default);

    /// <summary>Stop the tunnel and kill the process.</summary>
    Task StopAsync();
}
