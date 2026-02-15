using System.Net;
using System.Runtime.CompilerServices;
using ClawSharp.Core.Providers;
using ClawSharp.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClawSharp.Providers.Tests;

/// <summary>
/// Test helper that tracks call count and throws on first N calls
/// </summary>
public class TrackingLlmProvider : ILlmProvider
{
    private readonly Func<LlmRequest, CancellationToken, Task<LlmResponse>> _completeFunc;
    private readonly string _name;
    public int CallCount { get; private set; }

    public TrackingLlmProvider(string name, Func<LlmRequest, CancellationToken, Task<LlmResponse>> completeFunc)
    {
        _name = name;
        _completeFunc = completeFunc;
    }

    public string Name => _name;

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default) 
        => Task.FromResult<IReadOnlyList<string>>([]);

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        CallCount++;
        return await _completeFunc(request, ct);
    }

    public IAsyncEnumerable<LlmStreamChunk> StreamAsync(LlmRequest request, CancellationToken ct = default)
        => throw new NotImplementedException();
}

public class ResilientProviderTests
{
    [Fact]
    public async Task CompleteAsync_OnTransientFailure_Retries()
    {
        // Arrange
        var attemptCount = 0;
        var provider = new TrackingLlmProvider("test", async (req, ct) =>
        {
            attemptCount++;
            if (attemptCount == 1)
                throw new HttpRequestException("rate limited", null, HttpStatusCode.TooManyRequests);
            return new LlmResponse("Success", [], "stop", null);
        });
        
        var resilient = new ResilientProvider([provider], NullLogger<ResilientProvider>.Instance);
        
        // Act
        var result = await resilient.CompleteAsync(new LlmRequest { Model = "test", Messages = [new LlmMessage("user", "Hi")] });
        
        // Assert
        result.Content.Should().Be("Success");
        attemptCount.Should().Be(2);
    }

    [Fact]
    public async Task CompleteAsync_OnPermanentFailure_FallsBackToNextProvider()
    {
        // Arrange
        var primary = new TrackingLlmProvider("primary", (req, ct) => throw new InvalidOperationException("API key invalid"));
        
        var fallback = new TrackingLlmProvider("fallback", (req, ct) => 
            Task.FromResult(new LlmResponse("Fallback response", [], "stop", null)));
        
        var resilient = new ResilientProvider([primary, fallback], NullLogger<ResilientProvider>.Instance);
        
        // Act
        var result = await resilient.CompleteAsync(new LlmRequest { Model = "test", Messages = [new LlmMessage("user", "Hi")] });
        
        // Assert
        result.Content.Should().Be("Fallback response");
    }

    [Fact]
    public async Task CompleteAsync_AllProvidersExhausted_Throws()
    {
        // Arrange
        var provider = new TrackingLlmProvider("test", (req, ct) => throw new InvalidOperationException("fail"));
        
        var resilient = new ResilientProvider([provider], NullLogger<ResilientProvider>.Instance);
        
        // Act
        var act = () => resilient.CompleteAsync(new LlmRequest { Model = "test", Messages = [new LlmMessage("user", "Hi")] });
        
        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*All providers exhausted*");
    }

    [Fact]
    public void Name_ReturnsResilient()
    {
        var resilient = new ResilientProvider([], NullLogger<ResilientProvider>.Instance);
        resilient.Name.Should().Be("resilient");
    }

    [Fact]
    public async Task StreamAsync_ForwardsToProvider()
    {
        // Arrange
        var chunks = new List<LlmStreamChunk>
        {
            new LlmStreamChunk("Hello", null, null, null),
            new LlmStreamChunk(" world", null, null, null)
        };
        
        var provider = new TrackingLlmProvider("test", (req, ct) => Task.FromResult(new LlmResponse("test", [], "stop", null)));
        
        // We need to make StreamAsync work too - let's create a simple wrapper
        var providerMock = new SimpleMockProvider("test", chunks);
        
        var resilient = new ResilientProvider([providerMock], NullLogger<ResilientProvider>.Instance);
        
        // Act
        var resultChunks = new List<string>();
        await foreach (var chunk in resilient.StreamAsync(new LlmRequest { Model = "test", Messages = [new LlmMessage("user", "Hi")] }))
        {
            if (chunk.ContentDelta is not null)
                resultChunks.Add(chunk.ContentDelta);
        }
        
        // Assert
        resultChunks.Should().BeEquivalentTo(["Hello", " world"]);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsTrueIfAnyProviderAvailable()
    {
        // Arrange
        var unavailable = new SimpleMockProvider("unavailable", []);
        unavailable.SetAvailable(false);
        
        var available = new SimpleMockProvider("available", []);
        available.SetAvailable(true);
        
        var resilient = new ResilientProvider([unavailable, available], NullLogger<ResilientProvider>.Instance);
        
        // Act
        var result = await resilient.IsAvailableAsync();
        
        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsModelsFromFirstAvailable()
    {
        // Arrange
        var unavailable = new SimpleMockProvider("unavailable", []);
        unavailable.SetAvailable(false);
        
        var available = new SimpleMockProvider("available", [], new List<string> { "gpt-4", "gpt-3.5" });
        available.SetAvailable(true);
        
        var resilient = new ResilientProvider([unavailable, available], NullLogger<ResilientProvider>.Instance);
        
        // Act
        var models = await resilient.ListModelsAsync();
        
        // Assert
        models.Should().Contain("gpt-4");
    }
}

/// <summary>
/// Simple mock provider for testing
/// </summary>
public class SimpleMockProvider : ILlmProvider
{
    private readonly IReadOnlyList<LlmStreamChunk> _streamChunks;
    private readonly List<string> _models;
    private bool _isAvailable = true;

    public string Name { get; }

    public SimpleMockProvider(string name, IReadOnlyList<LlmStreamChunk> streamChunks, List<string>? models = null)
    {
        Name = name;
        _streamChunks = streamChunks;
        _models = models ?? [];
    }

    public void SetAvailable(bool available) => _isAvailable = available;

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(_isAvailable);

    public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default) 
        => Task.FromResult<IReadOnlyList<string>>(_models);

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
        => Task.FromResult(new LlmResponse("Mock response", [], "stop", null));

    public IAsyncEnumerable<LlmStreamChunk> StreamAsync(LlmRequest request, CancellationToken ct = default)
        => _streamChunks.ToAsyncEnumerable();
}
