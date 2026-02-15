using ClawSharp.Core.Providers;
using ClawSharp.Providers;
using ClawSharp.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace ClawSharp.Providers.Tests;

public class AnthropicProviderTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly AnthropicProvider _provider;

    public AnthropicProviderTests()
    {
        var client = _handler.CreateClient("https://api.anthropic.com/");
        _provider = new AnthropicProvider(client, NullLogger<AnthropicProvider>.Instance);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsContent()
    {
        _handler.EnqueueJson("""
        { "content": [{"type": "text", "text": "Hello!"}], "stop_reason": "end_turn",
          "usage": {"input_tokens": 10, "output_tokens": 5} }
        """);
        var request = new LlmRequest { Model = "claude-sonnet-4-20250514", Messages = [new("user", "Hi")] };
        var response = await _provider.CompleteAsync(request);
        response.Content.Should().Be("Hello!");
        response.FinishReason.Should().Be("end_turn");
    }

    [Fact]
    public async Task CompleteAsync_MapsSystemPromptToTopLevel()
    {
        _handler.EnqueueJson("""
        { "content": [{"type": "text", "text": "Hello!"}], "stop_reason": "end_turn",
          "usage": {"input_tokens": 10, "output_tokens": 5} }
        """);
        var request = new LlmRequest { Model = "claude-sonnet-4-20250514",
            Messages = [new("system", "Be helpful"), new("user", "Hi")] };
        var response = await _provider.CompleteAsync(request);
        response.Content.Should().Be("Hello!");
        // Verify system was sent as top-level field, not in messages
        _handler.SentRequests.Should().HaveCount(1);
        var requestBody = await _handler.SentRequests[0].Content!.ReadAsStringAsync();
        requestBody.Should().Contain("\"system\"");
        requestBody.Should().NotContain("\"role\": \"system\"");
    }

    [Fact]
    public async Task CompleteAsync_ParsesToolUseBlocks()
    {
        _handler.EnqueueJson("""
        { "content": [{"type": "tool_use", "id": "tu_1", "name": "shell",
          "input": {"command": "date"}}], "stop_reason": "tool_use" }
        """);
        var request = new LlmRequest { Model = "claude-sonnet-4-20250514", Messages = [new("user", "time?")] };
        var response = await _provider.CompleteAsync(request);
        response.ToolCalls.Should().HaveCount(1);
        response.ToolCalls[0].Name.Should().Be("shell");
        response.ToolCalls[0].Id.Should().Be("tu_1");
        response.ToolCalls[0].ArgumentsJson.Should().Contain("date");
    }

    [Fact]
    public async Task CompleteAsync_MapsToolResultsCorrectly()
    {
        _handler.EnqueueJson("""
        { "content": [{"type": "text", "text": "It's 3pm"}], "stop_reason": "end_turn" }
        """);
        var messages = new List<LlmMessage> {
            new("user", "time?"),
            new("assistant", "", [new("tu_1", "shell", "{}")]),
            new("tool", "3:00 PM", ToolCallId: "tu_1", Name: "shell")
        };
        var request = new LlmRequest { Model = "claude-sonnet-4-20250514", Messages = messages };
        await _provider.CompleteAsync(request);
        
        // Verify tool result mapped to content block with type "tool_result"
        var requestBody = await _handler.SentRequests[0].Content!.ReadAsStringAsync();
        requestBody.Should().Contain("\"type\":\"tool_result\"");
    }

    [Fact]
    public async Task IsAvailableAsync_WhenApiReachable_ReturnsTrue()
    {
        _handler.EnqueueJson("""{ "content": [] }""");
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
    public void Name_ReturnsAnthropic()
    {
        _provider.Name.Should().Be("anthropic");
    }
}
