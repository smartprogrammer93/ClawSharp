using ClawSharp.Channels;
using ClawSharp.Core.Channels;
using ClawSharp.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ClawSharp.Core.Tests.Channels;

public class ChannelManagerTests
{
    private sealed class TestChannel : IChannel
    {
        public string Name { get; init; } = "test";
        public event Func<ChannelMessage, Task>? OnMessage;
        
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
        public Task SendAsync(OutboundMessage message, CancellationToken ct = default) => Task.CompletedTask;
        
        public Task SimulateMessage(ChannelMessage message) => OnMessage?.Invoke(message) ?? Task.CompletedTask;
    }

    [Fact]
    public async Task StartAsync_StartsAllChannels()
    {
        // Arrange
        var ch1 = Substitute.For<IChannel>();
        ch1.Name.Returns("telegram");
        var ch2 = Substitute.For<IChannel>();
        ch2.Name.Returns("discord");
        
        var manager = new ChannelManager(
            new[] { ch1, ch2 },
            new InProcessMessageBus(),
            NullLogger<ChannelManager>.Instance);
        
        // Act
        await manager.StartAsync(CancellationToken.None);
        
        // Assert
        await ch1.Received(1).StartAsync(Arg.Any<CancellationToken>());
        await ch2.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_FailedChannel_ContinuesOthers()
    {
        // Arrange
        var ch1 = Substitute.For<IChannel>();
        ch1.Name.Returns("bad");
        ch1.StartAsync(Arg.Any<CancellationToken>()).Returns(x => throw new Exception("fail"));
        
        var ch2 = Substitute.For<IChannel>();
        ch2.Name.Returns("good");
        
        var manager = new ChannelManager(
            new[] { ch1, ch2 },
            new InProcessMessageBus(),
            NullLogger<ChannelManager>.Instance);
        
        // Act
        await manager.StartAsync(CancellationToken.None);
        
        // Assert
        await ch2.Received(1).StartAsync(Arg.Any<CancellationToken>());
        manager.Statuses.Should().Contain(s => s.Name == "bad" && !s.IsRunning);
        manager.Statuses.Should().Contain(s => s.Name == "good" && s.IsRunning);
    }

    [Fact]
    public async Task StopAsync_StopsAllChannels()
    {
        // Arrange
        var ch1 = Substitute.For<IChannel>();
        ch1.Name.Returns("telegram");
        
        var manager = new ChannelManager(
            new[] { ch1 },
            new InProcessMessageBus(),
            NullLogger<ChannelManager>.Instance);
        
        await manager.StartAsync(CancellationToken.None);
        
        // Act
        await manager.StopAsync(CancellationToken.None);
        
        // Assert
        await ch1.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnMessage_PublishesToMessageBus()
    {
        // Arrange
        var ch = new TestChannel { Name = "test" };
        
        var bus = new InProcessMessageBus();
        ChannelMessage? received = null;
        bus.Subscribe<ChannelMessage>(msg => 
        {
            received = msg;
            return Task.CompletedTask;
        });
        
        var manager = new ChannelManager(
            new[] { ch },
            bus,
            NullLogger<ChannelManager>.Instance);
        
        await manager.StartAsync(CancellationToken.None);
        
        // Act
        var testMessage = new ChannelMessage(
            "1", "user", "hi", "test", "chat", DateTimeOffset.UtcNow);
        
        // Simulate channel receiving a message
        await ch.SimulateMessage(testMessage);
        
        // Wait a bit for the message to propagate
        await Task.Delay(100);
        
        // Assert
        received.Should().NotBeNull();
        received!.Id.Should().Be("1");
        received.Content.Should().Be("hi");
    }

    [Fact]
    public void Statuses_InitiallyEmpty()
    {
        // Arrange & Act
        var manager = new ChannelManager(
            Array.Empty<IChannel>(),
            new InProcessMessageBus(),
            NullLogger<ChannelManager>.Instance);
        
        // Assert
        manager.Statuses.Should().BeEmpty();
    }

    [Fact]
    public async Task StartAsync_SetsRunningStatus()
    {
        // Arrange
        var ch = Substitute.For<IChannel>();
        ch.Name.Returns("telegram");
        
        var manager = new ChannelManager(
            new[] { ch },
            new InProcessMessageBus(),
            NullLogger<ChannelManager>.Instance);
        
        // Act
        await manager.StartAsync(CancellationToken.None);
        
        // Assert
        manager.Statuses.Should().Contain(s => s.Name == "telegram" && s.IsRunning);
    }

    [Fact]
    public async Task StopAsync_ClearsRunningStatus()
    {
        // Arrange
        var ch = Substitute.For<IChannel>();
        ch.Name.Returns("telegram");
        
        var manager = new ChannelManager(
            new[] { ch },
            new InProcessMessageBus(),
            NullLogger<ChannelManager>.Instance);
        
        await manager.StartAsync(CancellationToken.None);
        
        // Act
        await manager.StopAsync(CancellationToken.None);
        
        // Assert
        manager.Statuses.Should().Contain(s => s.Name == "telegram" && !s.IsRunning);
    }
}
