using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ClawSharp.Core.Auth;

/// <summary>
/// Manages OAuth tokens: loading from auth-profiles.json, checking expiration, and refreshing.
/// </summary>
public class OAuthTokenManager
{
    private readonly string _profilesPath;
    private readonly string _profileId;
    private readonly ILogger<OAuthTokenManager>? _logger;
    private OAuthCredential? _cachedCredential;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    
    // Default buffer time before expiration to trigger refresh (5 minutes)
    private const long ExpiryBufferMs = 5 * 60 * 1000;
    
    // Anthropic OAuth token refresh endpoint
    private const string AnthropicTokenEndpoint = "https://auth.anthropic.com/oauth/token";
    
    public OAuthTokenManager(string profilesPath, string profileId, ILogger<OAuthTokenManager>? logger = null)
    {
        _profilesPath = profilesPath ?? throw new ArgumentNullException(nameof(profilesPath));
        _profileId = profileId ?? throw new ArgumentNullException(nameof(profileId));
        _logger = logger;
    }
    
    /// <summary>
    /// Creates an OAuthTokenManager with default OpenClaw auth-profiles path.
    /// </summary>
    public static OAuthTokenManager CreateForOpenClaw(string profileId, ILogger<OAuthTokenManager>? logger = null)
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var defaultPath = Path.Combine(homeDir, ".openclaw", "agents", "main", "agent", "auth-profiles.json");
        return new OAuthTokenManager(defaultPath, profileId, logger);
    }
    
    /// <summary>
    /// Gets the current access token, refreshing if necessary.
    /// Returns null if no valid credential is available.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync(HttpClient httpClient, CancellationToken ct = default)
    {
        var credential = await LoadCredentialAsync(ct);
        if (credential == null || string.IsNullOrEmpty(credential.Access))
        {
            _logger?.LogWarning("No OAuth credential found for profile {ProfileId}", _profileId);
            return null;
        }
        
        // Check if token is expired or near expiry
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (credential.Expires > 0 && credential.Expires <= now + ExpiryBufferMs)
        {
            _logger?.LogInformation("OAuth token is expired or near expiry, refreshing...");
            var refreshed = await RefreshTokenAsync(httpClient, ct);
            if (!refreshed)
            {
                _logger?.LogWarning("Failed to refresh OAuth token, returning expired token");
            }
        }
        
        return credential.Access;
    }
    
    /// <summary>
    /// Checks if the current credential is expired.
    /// </summary>
    public bool IsTokenExpired()
    {
        if (_cachedCredential == null) return true;
        if (_cachedCredential.Expires <= 0) return false; // No expiry set
        
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return _cachedCredential.Expires <= now;
    }
    
    /// <summary>
    /// Loads the credential from auth-profiles.json.
    /// </summary>
    public async Task<OAuthCredential?> LoadCredentialAsync(CancellationToken ct = default)
    {
        // Return cached if valid
        if (_cachedCredential != null && !IsTokenExpired())
        {
            return _cachedCredential;
        }
        
        if (!File.Exists(_profilesPath))
        {
            _logger?.LogWarning("Auth profiles file not found: {Path}", _profilesPath);
            return null;
        }
        
        try
        {
            var json = await File.ReadAllTextAsync(_profilesPath, ct);
            var store = JsonSerializer.Deserialize<AuthProfilesStore>(json);
            
            if (store?.Profiles == null || !store.Profiles.TryGetValue(_profileId, out var credential))
            {
                _logger?.LogWarning("Profile {ProfileId} not found in auth-profiles", _profileId);
                return null;
            }
            
            _cachedCredential = credential;
            return credential;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load auth profiles from {Path}", _profilesPath);
            return null;
        }
    }
    
    /// <summary>
    /// Refreshes the OAuth token using the refresh token.
    /// </summary>
    public async Task<bool> RefreshTokenAsync(HttpClient httpClient, CancellationToken ct = default)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            // Reload to get latest refresh token
            var credential = await LoadCredentialAsync(ct);
            if (credential == null || string.IsNullOrEmpty(credential.Refresh))
            {
                _logger?.LogWarning("No refresh token available for profile {ProfileId}", _profileId);
                return false;
            }
            
            // Determine the token endpoint based on provider
            var endpoint = GetTokenEndpoint(credential.Provider);
            
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = credential.Refresh
            });
            
            var response = await httpClient.PostAsync(endpoint, content, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger?.LogError("Token refresh failed: {Status} - {Error}", response.StatusCode, errorBody);
                return false;
            }
            
            var responseJson = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStreamAsync(ct));
            
            // Parse the response
            var newAccessToken = responseJson.TryGetProperty("access_token", out var accessTokenProp) 
                ? accessTokenProp.GetString() 
                : null;
            
            var newRefreshToken = responseJson.TryGetProperty("refresh_token", out var refreshTokenProp) 
                ? refreshTokenProp.GetString() 
                : credential.Refresh; // Keep old refresh token if not provided
            
            var expiresIn = responseJson.TryGetProperty("expires_in", out var expiresInProp) 
                ? expiresInProp.GetInt64() 
                : 3600; // Default 1 hour
            
            if (string.IsNullOrEmpty(newAccessToken))
            {
                _logger?.LogError("Token refresh response missing access_token");
                return false;
            }
            
            // Calculate new expiry time (with small buffer)
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var newExpires = now + (expiresIn * 1000) - ExpiryBufferMs;
            
            // Update cached credential
            _cachedCredential = new OAuthCredential
            {
                Type = credential.Type,
                Provider = credential.Provider,
                Access = newAccessToken,
                Refresh = newRefreshToken,
                Expires = newExpires,
                Email = credential.Email,
                EnterpriseUrl = credential.EnterpriseUrl,
                ProjectId = credential.ProjectId,
                AccountId = credential.AccountId
            };
            
            // Save updated token back to file
            await SaveCredentialAsync(_cachedCredential, ct);
            
            _logger?.LogInformation("OAuth token refreshed successfully, expires at {Expires}", 
                DateTimeOffset.FromUnixTimeMilliseconds(newExpires));
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception during token refresh");
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
    
    /// <summary>
    /// Saves the updated credential back to auth-profiles.json.
    /// </summary>
    private async Task SaveCredentialAsync(OAuthCredential credential, CancellationToken ct = default)
    {
        if (!File.Exists(_profilesPath))
        {
            _logger?.LogWarning("Auth profiles file not found, cannot save");
            return;
        }
        
        try
        {
            var json = await File.ReadAllTextAsync(_profilesPath, ct);
            var store = JsonSerializer.Deserialize<AuthProfilesStore>(json);
            
            if (store?.Profiles != null && _profileId != null)
            {
                store.Profiles[_profileId] = credential;
                
                var options = new JsonSerializerOptions { WriteIndented = true };
                var updatedJson = JsonSerializer.Serialize(store, options);
                await File.WriteAllTextAsync(_profilesPath, updatedJson, ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save updated auth profile");
        }
    }
    
    /// <summary>
    /// Gets the token endpoint URL for the given provider.
    /// </summary>
    private static string GetTokenEndpoint(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "anthropic" => AnthropicTokenEndpoint,
            "minimax-portal" => "https://api.minimax.io/v1/oauth/token",
            "qwen-portal" => "https://qianwen.aliyun.com/oauth/refresh_token",
            "chutes" => "https://auth.chutes.ai/oauth/token",
            _ => throw new NotSupportedException($"OAuth refresh not supported for provider: {provider}")
        };
    }
}
