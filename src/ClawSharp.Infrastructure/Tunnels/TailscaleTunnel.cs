using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ClawSharp.Infrastructure.Tunnels;

/// <summary>
/// Tailscale Funnel tunnel implementation.
/// Uses `tailscale funnel` to expose a local port via the Tailscale network.
/// </summary>
public sealed class TailscaleTunnel : ITunnel, IDisposable
{
    private readonly ILogger _logger;
    private Process? _process;

    public string Name => "tailscale";
    public string? PublicUrl { get; private set; }
    public bool IsRunning => _process is { HasExited: false };
    public string? Hostname { get; }

    public TailscaleTunnel(ILogger logger, string? hostname = null)
    {
        _logger = logger;
        Hostname = hostname;
    }

    public async Task StartAsync(int localPort, CancellationToken ct = default)
    {
        if (IsRunning)
        {
            _logger.LogWarning("Tailscale tunnel already running");
            return;
        }

        var psi = new ProcessStartInfo("tailscale", $"funnel {localPort}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = Process.Start(psi);
        if (_process == null)
            throw new InvalidOperationException("Failed to start tailscale funnel");

        // Get the Tailscale hostname for the public URL
        var tsHostname = Hostname ?? await GetTailscaleHostnameAsync(ct);
        if (tsHostname != null)
        {
            PublicUrl = $"https://{tsHostname}";
        }

        _logger.LogInformation("Tailscale funnel started: {Url}", PublicUrl);
    }

    public Task StopAsync()
    {
        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping tailscale funnel");
            }
        }

        PublicUrl = null;
        _process?.Dispose();
        _process = null;
        return Task.CompletedTask;
    }

    private static async Task<string?> GetTailscaleHostnameAsync(CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo("tailscale", "status --json")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            // Simple parse: look for "DNSName" in the JSON
            var dnsNameIdx = output.IndexOf("\"DNSName\"", StringComparison.Ordinal);
            if (dnsNameIdx < 0) return null;

            var colonIdx = output.IndexOf(':', dnsNameIdx);
            var quoteStart = output.IndexOf('"', colonIdx + 1);
            var quoteEnd = output.IndexOf('"', quoteStart + 1);

            if (quoteStart >= 0 && quoteEnd > quoteStart)
            {
                var dnsName = output[(quoteStart + 1)..quoteEnd].TrimEnd('.');
                return dnsName;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => StopAsync().GetAwaiter().GetResult();
}
