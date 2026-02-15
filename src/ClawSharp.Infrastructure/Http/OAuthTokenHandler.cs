using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using ClawSharp.Core.Auth;
using Microsoft.Extensions.Logging;

namespace ClawSharp.Infrastructure.Http;

/// <summary>
/// A DelegatingHandler that automatically refreshes OAuth tokens when they expire.
/// </summary>
public class OAuthTokenHandler : DelegatingHandler
{
    private readonly OAuthTokenManager _tokenManager;
    private readonly ILogger<OAuthTokenHandler>? _logger;
    private bool _isRefreshing;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private HttpClient? _tokenRefreshClient;
    
    // Number of times to retry after a 401 (token expired)
    private const int MaxRetries = 1;
    
    public OAuthTokenHandler(OAuthTokenManager tokenManager, ILogger<OAuthTokenHandler>? logger = null)
    {
        _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
        _logger = logger;
    }
    
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // Add the current access token to the request
        await AddAuthorizationHeaderAsync(request, ct);
        
        var response = await base.SendAsync(request, ct);
        
        // If we get a 401, try to refresh the token and retry
        if (response.StatusCode == HttpStatusCode.Unauthorized && !_isRefreshing)
        {
            var refreshed = await TryRefreshTokenAsync(ct);
            if (refreshed)
            {
                // Retry the request with new token
                response.Dispose();
                return await SendWithRetryAsync(request, ct);
            }
        }
        
        return response;
    }
    
    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage? lastResponse = null;
        
        for (int i = 0; i <= MaxRetries; i++)
        {
            await AddAuthorizationHeaderAsync(request, ct);
            var response = await base.SendAsync(request, ct);
            
            if (response.IsSuccessStatusCode || response.StatusCode != HttpStatusCode.Unauthorized)
            {
                // If we have a previous failed response, dispose it
                lastResponse?.Dispose();
                return response;
            }
            
            lastResponse?.Dispose();
            lastResponse = response;
            
            if (i < MaxRetries)
            {
                _logger?.LogWarning("Request failed with {Status}, attempt {Attempt}/{Max}", 
                    response.StatusCode, i + 1, MaxRetries + 1);
                
                var refreshed = await TryRefreshTokenAsync(ct);
                if (!refreshed)
                {
                    break;
                }
            }
        }
        
        // Last resort - return the last response
        return lastResponse ?? await base.SendAsync(request, ct);
    }
    
    private async Task AddAuthorizationHeaderAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // Create a temporary HttpClient for token refresh if needed
        var httpClient = GetOrCreateTokenRefreshClient();
        var token = await _tokenManager.GetAccessTokenAsync(httpClient, ct);
        
        if (!string.IsNullOrEmpty(token))
        {
            // Remove existing auth header if present
            request.Headers.Authorization = null;
            
            // Anthropic uses x-api-key header, but OAuth tokens go in Authorization header
            // Check if this is an Anthropic request
            if (request.RequestUri?.Host.Contains("anthropic") == true)
            {
                // For Anthropic OAuth, use Authorization header with Bearer token
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            
            _logger?.LogDebug("Added OAuth token to request");
        }
    }
    
    private HttpClient GetOrCreateTokenRefreshClient()
    {
        if (_tokenRefreshClient == null && InnerHandler != null)
        {
            _tokenRefreshClient = new HttpClient(InnerHandler);
        }
        return _tokenRefreshClient ?? new HttpClient();
    }
    
    private async Task<bool> TryRefreshTokenAsync(CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            if (_isRefreshing)
            {
                // Another thread is already refreshing, wait a bit and return false
                // The caller will retry with the new token
                await Task.Delay(100, ct);
                return !_tokenManager.IsTokenExpired();
            }
            
            _isRefreshing = true;
            _logger?.LogInformation("Token expired, refreshing...");
            
            var httpClient = GetOrCreateTokenRefreshClient();
            var success = await _tokenManager.RefreshTokenAsync(httpClient, ct);
            
            if (success)
            {
                _logger?.LogInformation("Token refreshed successfully");
            }
            else
            {
                _logger?.LogWarning("Failed to refresh token");
            }
            
            return success;
        }
        finally
        {
            _isRefreshing = false;
            _refreshLock.Release();
        }
    }
}
