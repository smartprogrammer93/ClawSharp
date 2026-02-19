using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace ClawSharp.Infrastructure.Tunnels;

/// <summary>
/// ngrok tunnel implementation.
/// </summary>
public sealed class NgrokTunnel : ITunnel, IDisposable
{
    private readonly ILogger _logger;
    private Process? _process;

    public string Name => "ngrok";
    public string? PublicUrl { get; private set; }
    public bool IsRunning => _process is { HasExited: false };
    public string? AuthToken { get; }

    public NgrokTunnel(ILogger logger, string? authToken = null)
    {
        _logger = logger;
        AuthToken = authToken;
    }

    public async Task StartAsync(int localPort, CancellationToken ct = default)
    {
        if (IsRunning)
        {
            _logger.LogWarning("ngrok tunnel already running");
            return;
        }

        // Set auth token if provided
        if (!string.IsNullOrEmpty(AuthToken))
        {
            var authPsi = new ProcessStartInfo("ngrok", $"config add-authtoken {AuthToken}")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var authProcess = Process.Start(authPsi);
            authProcess?.WaitForExit(10000);
        }

        var psi = new ProcessStartInfo("ngrok", $"http {localPort} --log stdout --log-format json")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = Process.Start(psi);
        if (_process == null)
            throw new InvalidOperationException("Failed to start ngrok process");

        // Give ngrok time to start, then query API
        await Task.Delay(2000, ct);
        PublicUrl = await QueryNgrokApiAsync(ct);
        _logger.LogInformation("ngrok tunnel started: {Url}", PublicUrl);
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
                _logger.LogWarning(ex, "Error stopping ngrok");
            }
        }

        PublicUrl = null;
        _process?.Dispose();
        _process = null;
        return Task.CompletedTask;
    }

    private static async Task<string?> QueryNgrokApiAsync(CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient();
            var response = await client.GetFromJsonAsync<NgrokApiResponse>(
                "http://localhost:4040/api/tunnels", ct);
            return response?.Tunnels?.FirstOrDefault(t =>
                t.PublicUrl?.StartsWith("https://") == true)?.PublicUrl;
        }
        catch
        {
            return null;
        }
    }

    private record NgrokApiResponse(NgrokTunnelInfo[]? Tunnels);
    private record NgrokTunnelInfo(string? PublicUrl);

    public void Dispose() => StopAsync().GetAwaiter().GetResult();
}
