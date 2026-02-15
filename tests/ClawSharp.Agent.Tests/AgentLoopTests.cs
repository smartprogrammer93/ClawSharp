using System.Text.Json;
using ClawSharp.Core.Channels;
using ClawSharp.Core.Providers;
using ClawSharp.Core.Tools;
using ClawSharp.Infrastructure.Messaging;
using ClawSharp.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ToolSpec = ClawSharp.Core.Tools.ToolSpec;

namespace ClawSharp.Agent.Tests;

public class AgentLoopTests
{
    [Fact]
    public async Task RunAsync_SimpleResponse_ReturnsContent()
    {
        // Arrange
        var provider = Substitute.For<ILlmProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Hello!", [], "stop", null));
        var loop = new AgentLoop(
            provider,
            new ToolRegistry(),
            new InProcessMessageBus(),
            NullLogger<AgentLoop>.Instance);
        
        var request = new AgentLoop.AgentRequest("gpt-4o", [new LlmMessage("user", "Hi")]);

        // Act
        var result = await loop.RunAsync(request);

        // Assert
        result.Content.Should().Be("Hello!");
        result.ToolExecutions.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_WithToolCall_ExecutesToolAndContinues()
    {
        // Arrange
        var provider = Substitute.For<ILlmProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new LlmResponse("", [new("call_1", "shell", """{"command":"echo hi"}""")], "tool_calls", null),
                new LlmResponse("The output was: hi", [], "stop", null));
        
        var tools = new ToolRegistry();
        var fakeTool = Substitute.For<ITool>();
        fakeTool.Name.Returns("shell");
        fakeTool.Description.Returns("Run shell commands");
        var schema = JsonSerializer.Deserialize<JsonElement>("""{ "type": "object", "properties": {} }""");
        fakeTool.Specification.Returns(new ToolSpec("shell", "Run shell commands", schema!));
        
        fakeTool.ExecuteAsync(Arg.Any<JsonElement>(), Arg.Any<CancellationToken>())
            .Returns(new ToolResult(true, "hi"));
        tools.Register(fakeTool);
        
        var loop = new AgentLoop(
            provider,
            tools,
            new InProcessMessageBus(),
            NullLogger<AgentLoop>.Instance);
        
        var request = new AgentLoop.AgentRequest("gpt-4o", [new LlmMessage("user", "Say hi")]);

        // Act
        var result = await loop.RunAsync(request);

        // Assert
        result.Content.Should().Contain("hi");
        result.ToolExecutions.Should().HaveCount(1);
        result.ToolExecutions[0].ToolName.Should().Be("shell");
    }

    [Fact]
    public async Task RunAsync_UnknownTool_ReturnsErrorResult()
    {
        // Arrange
        var provider = Substitute.For<ILlmProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new LlmResponse("", [new("call_1", "nonexistent", "{}")], "tool_calls", null),
                new LlmResponse("Sorry", [], "stop", null));
        
        var loop = new AgentLoop(
            provider,
            new ToolRegistry(),
            new InProcessMessageBus(),
            NullLogger<AgentLoop>.Instance);
        
        var request = new AgentLoop.AgentRequest("gpt-4o", [new LlmMessage("user", "test")]);

        // Act
        var result = await loop.RunAsync(request);

        // Assert
        result.ToolExecutions.Should().HaveCount(1);
        result.ToolExecutions[0].Result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_MaxIterations_StopsGracefully()
    {
        // Arrange
        var provider = Substitute.For<ILlmProvider>();
        // Always return tool_calls to trigger loop
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("", [new("call_1", "shell", "{}")], "tool_calls", null));
        
        var tools = new ToolRegistry();
        var fakeTool = Substitute.For<ITool>();
        fakeTool.Name.Returns("shell");
        fakeTool.Description.Returns("Run shell");
        var schema = JsonSerializer.Deserialize<JsonElement>("""{ "type": "object", "properties": {} }""");
        fakeTool.Specification.Returns(new ToolSpec("shell", "Run shell", schema!));
        
        fakeTool.ExecuteAsync(Arg.Any<JsonElement>(), Arg.Any<CancellationToken>())
            .Returns(new ToolResult(true, "ok"));
        tools.Register(fakeTool);
        
        var loop = new AgentLoop(
            provider,
            tools,
            new InProcessMessageBus(),
            NullLogger<AgentLoop>.Instance,
            maxIterations: 3);
        
        var request = new AgentLoop.AgentRequest("gpt-4o", [new LlmMessage("user", "loop")]);

        // Act
        var result = await loop.RunAsync(request);

        // Assert
        result.ToolExecutions.Should().HaveCount(3);
        result.Content.Should().ContainEquivalentOf("maximum");
    }

    [Fact]
    public async Task RunAsync_ToolException_CapturedAsError()
    {
        // Arrange
        var provider = Substitute.For<ILlmProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new LlmResponse("", [new("call_1", "shell", "{}")], "tool_calls", null),
                new LlmResponse("Error noted", [], "stop", null));
        
        var tools = new ToolRegistry();
        var fakeTool = Substitute.For<ITool>();
        fakeTool.Name.Returns("shell");
        fakeTool.Description.Returns("Run shell");
        var schema = JsonSerializer.Deserialize<JsonElement>("""{ "type": "object", "properties": {} }""");
        fakeTool.Specification.Returns(new ToolSpec("shell", "Run shell", schema!));
        
        fakeTool.ExecuteAsync(Arg.Any<JsonElement>(), Arg.Any<CancellationToken>())
            .Returns<ToolResult>(x => throw new Exception("boom"));
        tools.Register(fakeTool);
        
        var loop = new AgentLoop(
            provider,
            tools,
            new InProcessMessageBus(),
            NullLogger<AgentLoop>.Instance);
        
        var request = new AgentLoop.AgentRequest("gpt-4o", [new LlmMessage("user", "test")]);

        // Act
        var result = await loop.RunAsync(request);

        // Assert
        result.ToolExecutions[0].Result.Success.Should().BeFalse();
        result.ToolExecutions[0].Result.Error.Should().Contain("boom");
    }

    [Fact]
    public async Task RunAsync_PublishesToolEvents()
    {
        // Arrange
        var provider = Substitute.For<ILlmProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new LlmResponse("", [new("call_1", "shell", "{}")], "tool_calls", null),
                new LlmResponse("done", [], "stop", null));
        
        var tools = new ToolRegistry();
        var fakeTool = Substitute.For<ITool>();
        fakeTool.Name.Returns("shell");
        fakeTool.Description.Returns("Run shell");
        var schema = JsonSerializer.Deserialize<JsonElement>("""{ "type": "object", "properties": {} }""");
        fakeTool.Specification.Returns(new ToolSpec("shell", "Run shell", schema!));
        
        fakeTool.ExecuteAsync(Arg.Any<JsonElement>(), Arg.Any<CancellationToken>())
            .Returns(new ToolResult(true, "ok"));
        tools.Register(fakeTool);
        
        var bus = new InProcessMessageBus();
        var events = new List<string>();
        bus.Subscribe<AgentLoop.ToolStartedEvent>(e => { events.Add("started"); return Task.CompletedTask; });
        bus.Subscribe<AgentLoop.ToolCompletedEvent>(e => { events.Add("completed"); return Task.CompletedTask; });
        
        var loop = new AgentLoop(
            provider,
            tools,
            bus,
            NullLogger<AgentLoop>.Instance);
        
        var request = new AgentLoop.AgentRequest("gpt-4o", [new LlmMessage("user", "test")]);

        // Act
        await loop.RunAsync(request);

        // Assert
        events.Should().BeEquivalentTo(["started", "completed"]);
    }

    [Fact]
    public async Task RunAsync_PassesToolsToProvider()
    {
        // Arrange
        var provider = Substitute.For<ILlmProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Hello!", [], "stop", null));
        
        var tools = new ToolRegistry();
        var fakeTool = Substitute.For<ITool>();
        fakeTool.Name.Returns("shell");
        fakeTool.Description.Returns("Run shell");
        var schema = JsonSerializer.Deserialize<JsonElement>("""{ "type": "object", "properties": {} }""");
        fakeTool.Specification.Returns(new ToolSpec("shell", "Run shell commands", schema!));
        tools.Register(fakeTool);
        
        var loop = new AgentLoop(
            provider,
            tools,
            new InProcessMessageBus(),
            NullLogger<AgentLoop>.Instance);
        
        var request = new AgentLoop.AgentRequest("gpt-4o", [new LlmMessage("user", "Hi")]);

        // Act
        await loop.RunAsync(request);

        // Assert
        await provider.Received(1).CompleteAsync(
            Arg.Is<LlmRequest>(r => r.Tools != null && r.Tools.Count == 1 && r.Tools[0].Name == "shell"),
            Arg.Any<CancellationToken>());
    }
}
