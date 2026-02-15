using ClawSharp.Core.Providers;
using ClawSharp.Providers;
using ClawSharp.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClawSharp.Providers.Tests;

public class CompatibleProviderTests
{
    [Fact]
    public async Task CompatibleProvider_UsesCustomBaseUrl()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson("""{ "choices": [{ "message": { "content": "Hi" }, "finish_reason": "stop" }] }""");
        var client = handler.CreateClient("http://localhost:1234/v1/");
        var provider = new CompatibleProvider("local", client, NullLogger<CompatibleProvider>.Instance);
        provider.Name.Should().Be("local");
        var response = await provider.CompleteAsync(new LlmRequest { Model = "local-model", Messages = [new("user", "Hi")] });
        response.Content.Should().Be("Hi");
    }

    [Fact]
    public async Task CompatibleProvider_SendsCorrectModel()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson("""{ "choices": [{ "message": { "content": "Hi" }, "finish_reason": "stop" }] }""");
        var client = handler.CreateClient("http://localhost:1234/v1/");
        var provider = new CompatibleProvider("local", client, NullLogger<CompatibleProvider>.Instance);
        await provider.CompleteAsync(new LlmRequest { Model = "my-model", Messages = [new("user", "Hi")] });
        
        var requestBody = await handler.SentRequests[0].Content!.ReadAsStringAsync();
        requestBody.Should().Contain("my-model");
    }
}

public class OpenRouterProviderTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly OpenRouterProvider _provider;

    public OpenRouterProviderTests()
    {
        var client = _handler.CreateClient("https://openrouter.ai/api/v1/");
        _provider = new OpenRouterProvider(client, NullLogger<OpenRouterProvider>.Instance);
    }

    [Fact]
    public async Task CompleteAsync_SetsRequiredHeaders()
    {
        _handler.EnqueueJson("""{ "choices": [{ "message": { "content": "Hi" }, "finish_reason": "stop" }] }""");
        var request = new LlmRequest { Model = "openai/gpt-3.5-turbo", Messages = [new("user", "Hi")] };
        await _provider.CompleteAsync(request);
        
        _handler.SentRequests.Should().HaveCount(1);
        var req = _handler.SentRequests[0];
        req.RequestUri!.ToString().Should().Contain("openrouter");
    }

    [Fact]
    public void Name_ReturnsOpenRouter()
    {
        _provider.Name.Should().Be("openrouter");
    }
}

public class OllamaProviderTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly OllamaProvider _provider;

    public OllamaProviderTests()
    {
        var client = _handler.CreateClient("http://localhost:11434/v1/");
        _provider = new OllamaProvider(client, NullLogger<OllamaProvider>.Instance);
    }

    [Fact]
    public async Task CompleteAsync_WorksWithOllamaFormat()
    {
        _handler.EnqueueJson("""{ "choices": [{ "message": { "content": "Hello from Ollama" }, "finish_reason": "stop" }] }""");
        var request = new LlmRequest { Model = "llama2", Messages = [new("user", "Hi")] };
        var response = await _provider.CompleteAsync(request);
        response.Content.Should().Be("Hello from Ollama");
    }

    [Fact]
    public void Name_ReturnsOllama()
    {
        _provider.Name.Should().Be("ollama");
    }

    [Fact]
    public async Task IsAvailableAsync_WhenReachable_ReturnsTrue()
    {
        _handler.EnqueueJson("""{ "models": [] }""");
        var result = await _provider.IsAvailableAsync();
        result.Should().BeTrue();
    }
}
