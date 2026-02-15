using ClawSharp.Core.Providers;
using ClawSharp.Providers;
using ClawSharp.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace ClawSharp.Providers.Tests;

public class MiniMaxProviderTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly MiniMaxProvider _provider;

    public MiniMaxProviderTests()
    {
        var client = _handler.CreateClient("https://api.minimax.io/v1");
        _provider = new MiniMaxProvider(client, NullLogger<MiniMaxProvider>.Instance, "test-group-id");
    }

    [Fact]
    public async Task CompleteAsync_ReturnsContent()
    {
        _handler.EnqueueJson("""
        { "choices": [{ "message": { "content": "Hello!" }, "finish_reason": "stop" }],
          "usage": { "prompt_tokens": 10, "completion_tokens": 5 } }
        """);
        var request = new LlmRequest { Model = "MiniMax-M2.5", Messages = [new("user", "Hi")] };
        var response = await _provider.CompleteAsync(request);
        response.Content.Should().Be("Hello!");
        response.FinishReason.Should().Be("stop");
    }

    [Fact]
    public async Task CompleteAsync_MapsSystemPromptToSystemPromptField()
    {
        _handler.EnqueueJson("""
        { "choices": [{ "message": { "content": "Hello!" }, "finish_reason": "stop" }],
          "usage": { "prompt_tokens": 10, "completion_tokens": 5 } }
        """);
        var request = new LlmRequest { Model = "MiniMax-M2.5",
            Messages = [new("system", "Be helpful"), new("user", "Hi")] };
        var response = await _provider.CompleteAsync(request);
        response.Content.Should().Be("Hello!");
        
        // Verify system was sent as system_prompt field, not in messages
        var requestBody = await _handler.SentRequests[0].Content!.ReadAsStringAsync();
        requestBody.Should().Contain("\"system_prompt\":\"Be helpful\"");
    }

    [Fact]
    public async Task CompleteAsync_ParsesToolCallBlocks()
    {
        _handler.EnqueueJson("""
        { "choices": [{ "message": { "content": [{"type": "tool_call", "id": "tu_1", "name": "shell",
          "input": {"command": "date"}}] }, "finish_reason": "tool_calls" }] }
        """);
        var request = new LlmRequest { Model = "MiniMax-M2.5", Messages = [new("user", "time?")] };
        var response = await _provider.CompleteAsync(request);
        response.ToolCalls.Should().HaveCount(1);
        response.ToolCalls[0].Name.Should().Be("shell");
        response.ToolCalls[0].Id.Should().Be("tu_1");
        response.ToolCalls[0].ArgumentsJson.Should().Contain("date");
    }

    [Fact]
    public async Task CompleteAsync_ParsesStringContent()
    {
        _handler.EnqueueJson("""
        { "choices": [{ "message": { "content": "Plain text response" }, "finish_reason": "stop" }] }
        """);
        var request = new LlmRequest { Model = "MiniMax-M2.5", Messages = [new("user", "Hi")] };
        var response = await _provider.CompleteAsync(request);
        response.Content.Should().Be("Plain text response");
    }

    [Fact]
    public async Task CompleteAsync_ParsesArrayContentWithTextBlocks()
    {
        _handler.EnqueueJson("""
        { "choices": [{ "message": { "content": [{"type": "text", "text": "Part1"}, {"type": "text", "text": "Part2"}] }, "finish_reason": "stop" }] }
        """);
        var request = new LlmRequest { Model = "MiniMax-M2.5", Messages = [new("user", "Hi")] };
        var response = await _provider.CompleteAsync(request);
        response.Content.Should().Be("Part1Part2");
    }

    [Fact]
    public async Task CompleteAsync_MapsToolResultsCorrectly()
    {
        _handler.EnqueueJson("""
        { "choices": [{ "message": { "content": "It's 3pm" }, "finish_reason": "stop" }] }
        """);
        var messages = new List<LlmMessage> {
            new("user", "time?"),
            new("assistant", "", [new("tu_1", "shell", "{}")]),
            new("tool", "3:00 PM", ToolCallId: "tu_1", Name: "shell")
        };
        var request = new LlmRequest { Model = "MiniMax-M2.5", Messages = messages };
        await _provider.CompleteAsync(request);
        
        // Verify tool result was converted to user message
        var requestBody = await _handler.SentRequests[0].Content!.ReadAsStringAsync();
        requestBody.Should().Contain("\"role\":\"user\"");
    }

    [Fact]
    public async Task IsAvailableAsync_WhenApiReachable_ReturnsTrue()
    {
        _handler.EnqueueJson("""{ "choices": [] }""");
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
    public async Task ListModelsAsync_ReturnsDefaultModels()
    {
        var models = await _provider.ListModelsAsync();
        models.Should().Contain("MiniMax-M2.1");
        models.Should().Contain("MiniMax-M2.5");
    }

    [Fact]
    public void Name_ReturnsMinimax()
    {
        _provider.Name.Should().Be("minimax");
    }

    [Fact]
    public async Task CompleteAsync_IncludesMaxTokens()
    {
        _handler.EnqueueJson("""
        { "choices": [{ "message": { "content": "Hello!" }, "finish_reason": "stop" }] }
        """);
        var request = new LlmRequest { Model = "MiniMax-M2.5", Messages = [new("user", "Hi")], MaxTokens = 100 };
        await _provider.CompleteAsync(request);
        
        var requestBody = await _handler.SentRequests[0].Content!.ReadAsStringAsync();
        requestBody.Should().Contain("\"max_tokens\":100");
    }
}
