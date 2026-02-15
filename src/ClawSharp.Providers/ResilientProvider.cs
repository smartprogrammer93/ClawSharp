using System.Net;
using System.Runtime.CompilerServices;
using ClawSharp.Core.Providers;
using Microsoft.Extensions.Logging;

namespace ClawSharp.Providers;

/// <summary>
/// Wraps an LLM provider with retry logic and fallback chain support.
/// </summary>
public class ResilientProvider : ILlmProvider
{
    private readonly IReadOnlyList<ILlmProvider> _providers;
    private readonly ILogger<ResilientProvider> _logger;
    private const int MaxRetries = 3;

    public string Name => "resilient";

    public ResilientProvider(IReadOnlyList<ILlmProvider> providers, ILogger<ResilientProvider> logger)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        foreach (var provider in _providers)
        {
            if (await provider.IsAvailableAsync(ct))
            {
                _logger.LogDebug("Provider {ProviderName} is available", provider.Name);
                return true;
            }
            _logger.LogDebug("Provider {ProviderName} is not available, trying next", provider.Name);
        }
        return false;
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
    {
        foreach (var provider in _providers)
        {
            if (await provider.IsAvailableAsync(ct))
            {
                _logger.LogDebug("Listing models from provider {ProviderName}", provider.Name);
                return await provider.ListModelsAsync(ct);
            }
        }
        return [];
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        var exceptions = new List<Exception>();
        
        for (int providerIndex = 0; providerIndex < _providers.Count; providerIndex++)
        {
            var provider = _providers[providerIndex];
            
            for (int retry = 0; retry < MaxRetries; retry++)
            {
                try
                {
                    _logger.LogDebug("Attempting completion with provider {ProviderName}, attempt {Attempt}", 
                        provider.Name, retry + 1);
                    return await provider.CompleteAsync(request, ct);
                }
                catch (HttpRequestException ex) when (IsTransient(ex))
                {
                    _logger.LogWarning(ex, "Transient error from {ProviderName}, retry {Retry}/{MaxRetries}", 
                        provider.Name, retry + 1, MaxRetries);
                    exceptions.Add(ex);
                    
                    if (retry < MaxRetries - 1)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, retry));
                        await Task.Delay(delay, ct);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Permanent failure - try next provider
                    _logger.LogWarning(ex, "Permanent error from {ProviderName}, falling back to next provider", 
                        provider.Name);
                    exceptions.Add(ex);
                    break; // Break retry loop, try next provider
                }
            }
        }
        
        throw new InvalidOperationException(
            $"All providers exhausted. Errors: {string.Join("; ", exceptions.Select(e => e.Message))}", 
            exceptions.FirstOrDefault());
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var exceptions = new List<Exception>();
        
        for (int providerIndex = 0; providerIndex < _providers.Count; providerIndex++)
        {
            var provider = _providers[providerIndex];
            
            var (success, chunks) = await TryStreamAsync(provider, request, ct, exceptions);
            
            if (success)
            {
                await foreach (var chunk in chunks)
                {
                    yield return chunk;
                }
                yield break;
            }
        }
        
        throw new InvalidOperationException(
            $"All providers exhausted during streaming. Errors: {string.Join("; ", exceptions.Select(e => e.Message))}",
            exceptions.FirstOrDefault());
    }

    private async Task<(bool Success, IAsyncEnumerable<LlmStreamChunk> Chunks)> TryStreamAsync(
        ILlmProvider provider, 
        LlmRequest request, 
        CancellationToken ct,
        List<Exception> exceptions)
    {
        try
        {
            _logger.LogDebug("Streaming with provider {ProviderName}", provider.Name);
            return (true, provider.StreamAsync(request, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Error streaming from {ProviderName}, trying next", provider.Name);
            exceptions.Add(ex);
            return (false, AsyncEnumerable.Empty<LlmStreamChunk>());
        }
    }

    private static bool IsTransient(HttpRequestException ex)
    {
        return ex.StatusCode switch
        {
            HttpStatusCode.TooManyRequests => true,        // 429
            HttpStatusCode.RequestTimeout => true,          // 408
            HttpStatusCode.InternalServerError => true,     // 500
            HttpStatusCode.BadGateway => true,             // 502
            HttpStatusCode.ServiceUnavailable => true,      // 503
            HttpStatusCode.GatewayTimeout => true,          // 504
            _ => ex.InnerException is TimeoutException
        };
    }
}
