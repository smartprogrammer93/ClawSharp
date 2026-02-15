using System.Security.Cryptography;
using System.Text.Json;

namespace ClawSharp.Infrastructure.Security;

/// <summary>
/// HMAC-based token exchange for device pairing.
/// Generates time-limited pairing tokens and tracks paired devices.
/// </summary>
public sealed class PairingGuard
{
    private readonly string _dataDir;
    private readonly string _pairedDevicesFile;
    private readonly HashSet<string> _pairedDevices = new();
    private readonly Dictionary<string, DateTimeOffset> _pendingTokens = new();
    private readonly object _lock = new();

    private const int TokenLength = 32;
    private static readonly TimeSpan DefaultTokenExpiry = TimeSpan.FromMinutes(5);

    public PairingGuard(string dataDir)
    {
        ArgumentNullException.ThrowIfNull(dataDir);

        _dataDir = dataDir;
        Directory.CreateDirectory(_dataDir);

        _pairedDevicesFile = Path.Combine(_dataDir, "paired_devices.json");
        LoadPairedDevices();
    }

    /// <summary>
    /// Generates a new pairing token that expires after the specified duration.
    /// </summary>
    /// <param name="expiry">Token lifetime. Defaults to 5 minutes.</param>
    /// <returns>Base64-encoded pairing token.</returns>
    public string GeneratePairingToken(TimeSpan? expiry = null)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(TokenLength);
        var token = Convert.ToBase64String(tokenBytes);
        var expiresAt = DateTimeOffset.UtcNow + (expiry ?? DefaultTokenExpiry);

        lock (_lock)
        {
            _pendingTokens[token] = expiresAt;
            CleanupExpiredTokens();
        }

        return token;
    }

    /// <summary>
    /// Validates a pairing token and registers the device if valid.
    /// Token is consumed after successful validation.
    /// </summary>
    /// <param name="token">The pairing token to validate.</param>
    /// <param name="deviceId">The device identifier to pair.</param>
    /// <returns>True if pairing succeeded, false otherwise.</returns>
    public Task<bool> ValidateAndPairAsync(string token, string deviceId)
    {
        lock (_lock)
        {
            CleanupExpiredTokens();

            if (!_pendingTokens.TryGetValue(token, out var expiresAt))
                return Task.FromResult(false);

            if (DateTimeOffset.UtcNow > expiresAt)
            {
                _pendingTokens.Remove(token);
                return Task.FromResult(false);
            }

            // Consume the token
            _pendingTokens.Remove(token);

            // Register the device
            _pairedDevices.Add(deviceId);
            SavePairedDevices();

            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Checks if a device is currently paired.
    /// </summary>
    public bool IsDevicePaired(string deviceId)
    {
        lock (_lock)
        {
            return _pairedDevices.Contains(deviceId);
        }
    }

    /// <summary>
    /// Returns all currently paired device IDs.
    /// </summary>
    public IReadOnlyList<string> GetPairedDevices()
    {
        lock (_lock)
        {
            return _pairedDevices.ToList();
        }
    }

    /// <summary>
    /// Removes a device from the paired list.
    /// </summary>
    public void UnpairDevice(string deviceId)
    {
        lock (_lock)
        {
            _pairedDevices.Remove(deviceId);
            SavePairedDevices();
        }
    }

    private void CleanupExpiredTokens()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = _pendingTokens.Where(kv => now > kv.Value).Select(kv => kv.Key).ToList();
        foreach (var token in expired)
        {
            _pendingTokens.Remove(token);
        }
    }

    private void LoadPairedDevices()
    {
        if (!File.Exists(_pairedDevicesFile)) return;

        try
        {
            var json = File.ReadAllText(_pairedDevicesFile);
            var devices = JsonSerializer.Deserialize<List<string>>(json);
            if (devices != null)
            {
                foreach (var device in devices)
                {
                    _pairedDevices.Add(device);
                }
            }
        }
        catch
        {
            // Ignore corrupt files
        }
    }

    private void SavePairedDevices()
    {
        var json = JsonSerializer.Serialize(_pairedDevices.ToList(), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_pairedDevicesFile, json);
    }
}
