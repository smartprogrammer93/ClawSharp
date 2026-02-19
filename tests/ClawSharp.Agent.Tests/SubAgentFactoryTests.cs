using ClawSharp.Agent;
using ClawSharp.Core.Channels;
using ClawSharp.Core.Providers;
using ClawSharp.Core.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ClawSharp.Agent.Tests;

public class SubAgentFactoryTests
{
    private static (SubAgentFactory factory, ILlmProvider provider) CreateFactory(
        int maxConcurrent = 5,
        bool shouldFail = false,
        bool slow = false)
    {
        var provider = Substitute.For<ILlmProvider>();
        var tools = Substitute.For<IToolRegistry>();
        tools.GetSpecifications().Returns([]);
        var messageBus = Substitute.For<IMessageBus>();
        var logger = Substitute.For<ILogger<SubAgentFactory>>();

        if (shouldFail)
        {
            provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
                .Returns<LlmResponse>(_ => throw new Exception("Provider error"));
        }
        else if (slow)
        {
            provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
                .Returns(async call =>
                {
                    await Task.Delay(5000, call.Arg<CancellationToken>());
                    return new LlmResponse("Done", [], "stop", null);
                });
        }
        else
        {
            provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
                .Returns(new LlmResponse("Hello from sub-agent", [], "stop", null));
        }

        var factory = new SubAgentFactory(provider, tools, messageBus, logger, maxConcurrent);
        return (factory, provider);
    }

    [Fact]
    public async Task SpawnAsync_ReturnsResult()
    {
        var (factory, _) = CreateFactory();
        var request = new SubAgentRequest("Say hello");

        var result = await factory.SpawnAsync(request);

        result.Should().NotBeNull();
        result.SessionId.Should().StartWith("subagent:");
        result.Success.Should().BeTrue();
        result.Content.Should().Be("Hello from sub-agent");
    }

    [Fact]
    public async Task SpawnAsync_ExceedsMax_Throws()
    {
        var (factory, _) = CreateFactory(maxConcurrent: 1, slow: true);

        // Start a slow task
        var task1 = Task.Run(() => factory.SpawnAsync(new SubAgentRequest("task 1")));
        await Task.Delay(100); // let it start

        // Second should throw
        var act = () => factory.SpawnAsync(new SubAgentRequest("task 2"));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Max concurrent*");
    }

    [Fact]
    public async Task SpawnAsync_AgentError_ReturnsFailure()
    {
        var (factory, _) = CreateFactory(shouldFail: true);

        var result = await factory.SpawnAsync(new SubAgentRequest("fail"));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Provider error");
    }

    [Fact]
    public async Task SpawnAsync_CustomModel_UsedInRequest()
    {
        var (factory, provider) = CreateFactory();

        var result = await factory.SpawnAsync(
            new SubAgentRequest("task", Model: "claude-sonnet-4-20250514"));

        result.Success.Should().BeTrue();
        await provider.Received(1).CompleteAsync(
            Arg.Is<LlmRequest>(r => r.Model == "claude-sonnet-4-20250514"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SpawnAsync_DefaultModel_UsesDefault()
    {
        var (factory, provider) = CreateFactory();

        await factory.SpawnAsync(new SubAgentRequest("task"));

        await provider.Received(1).CompleteAsync(
            Arg.Is<LlmRequest>(r => r.Model == "default"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SpawnAsync_WithSystemPrompt_IncludesInMessages()
    {
        var (factory, provider) = CreateFactory();

        await factory.SpawnAsync(new SubAgentRequest("task", SystemPrompt: "You are a helper."));

        await provider.Received(1).CompleteAsync(
            Arg.Is<LlmRequest>(r =>
                r.Messages.Any(m => m.Role == "system" && m.Content.Contains("You are a helper."))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActiveCount_TracksRunningAgents()
    {
        var (factory, _) = CreateFactory();
        factory.ActiveCount.Should().Be(0);

        await factory.SpawnAsync(new SubAgentRequest("task"));

        // After completion, count should be back to 0
        factory.ActiveCount.Should().Be(0);
    }

    [Fact]
    public async Task SpawnAsync_WithCancellation_ThrowsCancelled()
    {
        var (factory, _) = CreateFactory(slow: true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => factory.SpawnAsync(new SubAgentRequest("task"), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CompletedSessions_TracksHistory()
    {
        var (factory, _) = CreateFactory();

        await factory.SpawnAsync(new SubAgentRequest("task 1"));
        await factory.SpawnAsync(new SubAgentRequest("task 2"));

        factory.CompletedSessions.Should().HaveCount(2);
    }

    [Fact]
    public void MaxConcurrent_ReturnsConfiguredValue()
    {
        var (factory, _) = CreateFactory(maxConcurrent: 10);
        factory.MaxConcurrent.Should().Be(10);
    }
}
