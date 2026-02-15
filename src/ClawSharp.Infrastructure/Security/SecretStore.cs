using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClawSharp.Infrastructure.Security;

/// <summary>
/// Encrypted secret storage using AES-GCM with file-based persistence.
/// Secrets are stored in individual encrypted files with chmod 600 permissions on Unix.
/// </summary>
public sealed class SecretStore
{
    private readonly string _secretsDir;
    private readonly byte[] _encryptionKey;
    private readonly Dictionary<string, string> _cache = new();
    private readonly object _lock = new();

    private const int KeySize = 32; // 256-bit
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public SecretStore(string dataDir)
    {
        ArgumentNullException.ThrowIfNull(dataDir);

        _secretsDir = Path.Combine(dataDir, "secrets");
        Directory.CreateDirectory(_secretsDir);

        var keyPath = Path.Combine(dataDir, ".secret_key");
        _encryptionKey = LoadOrCreateKey(keyPath);

        LoadAllSecrets();
    }

    /// <summary>Sets a secret value. Setting null removes the key.</summary>
    public void Set(string key, string? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        lock (_lock)
        {
            if (value is null)
            {
                Remove(key);
                return;
            }

            _cache[key] = value;
            SaveSecret(key, value);
        }
    }

    /// <summary>Gets a secret value, or null if not found.</summary>
    public string? Get(string key)
    {
        lock (_lock)
        {
            return _cache.TryGetValue(key, out var value) ? value : null;
        }
    }

    /// <summary>Removes a secret.</summary>
    public void Remove(string key)
    {
        lock (_lock)
        {
            _cache.Remove(key);
            var filePath = GetSecretFilePath(key);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>Returns true if the key exists.</summary>
    public bool Exists(string key)
    {
        lock (_lock)
        {
            return _cache.ContainsKey(key);
        }
    }

    /// <summary>Returns all stored keys.</summary>
    public IReadOnlyList<string> GetAllKeys()
    {
        lock (_lock)
        {
            return _cache.Keys.ToList();
        }
    }

    private void SaveSecret(string key, string value)
    {
        var filePath = GetSecretFilePath(key);
        var encrypted = Encrypt(value);
        File.WriteAllBytes(filePath, encrypted);
        SetRestrictedPermissions(filePath);
    }

    private void LoadAllSecrets()
    {
        if (!Directory.Exists(_secretsDir)) return;

        foreach (var file in Directory.GetFiles(_secretsDir, "*.enc"))
        {
            try
            {
                var keyName = DecodeKeyFromFileName(Path.GetFileNameWithoutExtension(file));
                var encrypted = File.ReadAllBytes(file);
                var value = Decrypt(encrypted);
                _cache[keyName] = value;
            }
            catch
            {
                // Skip corrupted files
            }
        }
    }

    private string GetSecretFilePath(string key)
    {
        var safeFileName = EncodeKeyAsFileName(key);
        return Path.Combine(_secretsDir, safeFileName + ".enc");
    }

    private static string EncodeKeyAsFileName(string key)
    {
        // Base64 URL-safe encoding
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(key))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string DecodeKeyFromFileName(string fileName)
    {
        var base64 = fileName.Replace('-', '+').Replace('_', '/');
        var padding = (4 - base64.Length % 4) % 4;
        base64 += new string('=', padding);
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }

    private byte[] Encrypt(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_encryptionKey, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Format: nonce || ciphertext || tag
        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);

        return result;
    }

    private string Decrypt(byte[] data)
    {
        if (data.Length < NonceSize + TagSize)
            throw new CryptographicException("Invalid encrypted data");

        var nonce = new byte[NonceSize];
        var ciphertextLength = data.Length - NonceSize - TagSize;
        var ciphertext = new byte[ciphertextLength];
        var tag = new byte[TagSize];

        Buffer.BlockCopy(data, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(data, NonceSize, ciphertext, 0, ciphertextLength);
        Buffer.BlockCopy(data, NonceSize + ciphertextLength, tag, 0, TagSize);

        var plaintext = new byte[ciphertextLength];
        using var aes = new AesGcm(_encryptionKey, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    private static byte[] LoadOrCreateKey(string keyPath)
    {
        if (File.Exists(keyPath))
        {
            var keyData = File.ReadAllBytes(keyPath);
            if (keyData.Length == KeySize)
                return keyData;
        }

        var newKey = RandomNumberGenerator.GetBytes(KeySize);
        File.WriteAllBytes(keyPath, newKey);
        SetRestrictedPermissions(keyPath);
        return newKey;
    }

    private static void SetRestrictedPermissions(string filePath)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
