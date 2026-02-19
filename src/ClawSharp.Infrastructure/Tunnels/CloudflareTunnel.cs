using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ClawSharp.Infrastructure.Tunnels;

/// <summary>
/// Cloudflare Tunnel (cloudflared) implementation.
/// Supports both quick tunnels (no token) and named tunnels (with token).
/// </summary>
public sealed partial class CloudflareTunnel : ITunnel, IDisposable
{
    private readonly ILogger _logger;
    private Process? _process;

    public string Name => "cloudflare";
    public string? PublicUrl { get; private set; }
    public bool IsRunning => _process is { HasExited: false };
    public string? Token { get; }
    public string? Domain { get; }

    public CloudflareTunnel(ILogger logger, string? token = null, string? domain = null)
    {
        _logger = logger;
        Token = token;
        Domain = domain;
    }

    public async Task StartAsync(int localPort, CancellationToken ct = default)
    {
        if (IsRunning)
        {
            _logger.LogWarning("Cloudflare tunnel already running");
            return;
        }

        var args = BuildArguments(localPort);
        _logger.LogInformation("Starting cloudflared with args: {Args}", args);

        var psi = new ProcessStartInfo("cloudflared", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = Process.Start(psi);
        if (_process == null)
            throw new InvalidOperationException("Failed to start cloudflared process");

        // Parse URL from stderr (cloudflared outputs there)
        PublicUrl = await ParsePublicUrlAsync(_process, ct);
        _logger.LogInformation("Cloudflare tunnel started: {Url}", PublicUrl);
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
                _logger.LogWarning(ex, "Error stopping cloudflared");
            }
        }

        PublicUrl = null;
        _process?.Dispose();
        _process = null;
        return Task.CompletedTask;
    }

    private string BuildArguments(int localPort)
    {
        if (!string.IsNullOrEmpty(Token))
            return $"tunnel run --token {Token}";

        var args = $"tunnel --url http://localhost:{localPort} --no-autoupdate";
        if (!string.IsNullOrEmpty(Domain))
            args += $" --hostname {Domain}";
        return args;
    }

    private static async Task<string?> ParsePublicUrlAsync(Process process, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<string?>();
        using var registration = ct.Register(() => tcs.TrySetCanceled(ct));

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            var match = UrlPattern().Match(e.Data);
            if (match.Success)
                tcs.TrySetResult(match.Value);
        };

        process.BeginErrorReadLine();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30), ct));
        if (completed == tcs.Task)
            return await tcs.Task;

        return null;
    }

    [GeneratedRegex(@"https://[a-zA-Z0-9\-]+\.trycloudflare\.com")]
    private static partial Regex UrlPattern();

    public void Dispose() => StopAsync().GetAwaiter().GetResult();
}
