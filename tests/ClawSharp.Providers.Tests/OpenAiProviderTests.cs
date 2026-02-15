using ClawSharp.Core.Providers;
using ClawSharp.Providers;
using ClawSharp.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace ClawSharp.Providers.Tests;

public class OpenAiProviderTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly OpenAiProvider _provider;

    public OpenAiProviderTests()
    {
        var client = _handler.CreateClient("https://api.openai.com/v1/");
        _provider = new OpenAiProvider(client, NullLogger<OpenAiProvider>.Instance);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsContent()
    {
        _handler.EnqueueJson("""
        { "choices": [{ "message": { "content": "Hello!" }, "finish_reason": "stop" }],
          "usage": { "prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15 } }
        """);
        var request = new LlmRequest { Model = "gpt-4o", Messages = [new("user", "Hi")] };
        var response = await _provider.CompleteAsync(request);
        response.Content.Should().Be("Hello!");
        response.FinishReason.Should().Be("stop");
        response.Usage!.TotalTokens.Should().Be(15);
    }

    [Fact]
    public async Task CompleteAsync_WithToolCalls_ParsesCorrectly()
    {
        _handler.EnqueueJson("""
        { "choices": [{ "message": { "content": "",
            "tool_calls": [{ "id": "call_123",
              "function": { "name": "shell", "arguments": "{\"command\":\"date\"}" } }] },
            "finish_reason": "tool_calls" }] }
        """);
        var request = new LlmRequest { Model = "gpt-4o", Messages = [new("user", "What time?")] };
        var response = await _provider.CompleteAsync(request);
        response.ToolCalls.Should().HaveCount(1);
        response.ToolCalls[0].Name.Should().Be("shell");
        response.FinishReason.Should().Be("tool_calls");
    }

    [Fact]
    public async Task IsAvailableAsync_WhenApiReachable_ReturnsTrue()
    {
        _handler.EnqueueJson("""{ "data": [] }""");
        var result = await _provider.IsAvailableAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WhenApiUnreachable_ReturnsFalse()
    {
        _handler.EnqueueError(System.Net.HttpStatusCode.ServiceUnavailable);
        var result = await _provider.IsAvailableAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsModelIds()
    {
        _handler.EnqueueJson("""{ "data": [{"id": "gpt-4o"}, {"id": "gpt-3.5-turbo"}] }""");
        var models = await _provider.ListModelsAsync();
        models.Should().Contain("gpt-4o");
        models.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Name_ReturnsOpenai()
    {
        _provider.Name.Should().Be("openai");
    }
}
