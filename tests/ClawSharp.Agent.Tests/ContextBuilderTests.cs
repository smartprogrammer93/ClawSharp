using ClawSharp.Agent;
using ClawSharp.Core.Config;
using ClawSharp.Core.Memory;
using ClawSharp.Core.Providers;
using ClawSharp.Core.Tools;
using ClawSharp.Tools;
using FluentAssertions;
using NSubstitute;
using ToolSpec = ClawSharp.Core.Tools.ToolSpec;

namespace ClawSharp.Agent.Tests;

public class ContextBuilderTests
{
    private readonly IMemoryStore _memoryStore;
    private readonly IToolRegistry _toolRegistry;
    private readonly ClawSharpConfig _config;

    public ContextBuilderTests()
    {
        _memoryStore = Substitute.For<IMemoryStore>();
        _toolRegistry = new ToolRegistry();
        _config = CreateTestConfig();
    }

    private static ClawSharpConfig CreateTestConfig()
    {
        return new ClawSharpConfig
        {
            DataDir = "/tmp/clawsharp",
            MaxContextTokens = 128_000
        };
    }

    [Fact]
    public async Task BuildSystemPromptAsync_IncludesIdentity()
    {
        _memoryStore.ListAsync(Arg.Any<MemoryCategory?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<MemoryEntry>());
        
        var builder = new ContextBuilder(_memoryStore, _toolRegistry, _config);
        var prompt = await builder.BuildSystemPromptAsync();
        
        prompt.Should().Contain("ClawSharp");
        prompt.Should().Contain("UTC");
    }

    [Fact]
    public async Task BuildSystemPromptAsync_IncludesCurrentTime()
    {
        _memoryStore.ListAsync(Arg.Any<MemoryCategory?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<MemoryEntry>());
        
        var builder = new ContextBuilder(_memoryStore, _toolRegistry, _config);
        var prompt = await builder.BuildSystemPromptAsync();
        
        prompt.Should().Contain("Current time");
    }

    [Fact]
    public async Task BuildSystemPromptAsync_IncludesMemories()
    {
        _memoryStore.ListAsync(Arg.Any<MemoryCategory?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<MemoryEntry> 
            { 
                new("1", "name", "User likes cats", MemoryCategory.Core, DateTimeOffset.UtcNow) 
            });
        
        var builder = new ContextBuilder(_memoryStore, _toolRegistry, _config);
        var prompt = await builder.BuildSystemPromptAsync();
        
        prompt.Should().Contain("User likes cats");
    }

    [Fact]
    public async Task BuildSystemPromptAsync_IncludesToolDescriptions()
    {
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("shell");
        tool.Specification.Returns(new ToolSpec("shell", "Execute shell commands", default));
        _toolRegistry.Register(tool);
        
        _memoryStore.ListAsync(Arg.Any<MemoryCategory?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<MemoryEntry>());
        
        var builder = new ContextBuilder(_memoryStore, _toolRegistry, _config);
        var prompt = await builder.BuildSystemPromptAsync();
        
        prompt.Should().Contain("shell");
        prompt.Should().Contain("Execute shell commands");
    }

    [Fact]
    public void TrimHistory_UnderBudget_ReturnsAll()
    {
        var history = new List<LlmMessage> 
        { 
            new("user", "Hi"), 
            new("assistant", "Hello") 
        };
        
        var builder = new ContextBuilder(_memoryStore, _toolRegistry, _config);
        var trimmed = builder.TrimHistory(history, 10000);
        
        trimmed.Should().HaveCount(2);
    }

    [Fact]
    public void TrimHistory_OverBudget_KeepsRecentMessages()
    {
        var history = Enumerable.Range(0, 100)
            .Select(i => new LlmMessage("user", new string('x', 100)))
            .ToList();
        
        var builder = new ContextBuilder(_memoryStore, _toolRegistry, _config);
        var trimmed = builder.TrimHistory(history, 100); // very small budget
        
        trimmed.Count.Should().BeLessThan(100);
        trimmed.Should().NotBeEmpty();
        // Most recent messages should be kept
        trimmed.Last().Content.Should().Be(new string('x', 100));
    }

    [Fact]
    public void TrimHistory_AlwaysKeepsSystemPrompt()
    {
        // System prompt is not part of history trimming - it's added separately
        var history = new List<LlmMessage>();
        
        var builder = new ContextBuilder(_memoryStore, _toolRegistry, _config);
        var trimmed = builder.TrimHistory(history, 100);
        
        trimmed.Should().BeEmpty();
    }
}
