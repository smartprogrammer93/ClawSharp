using ClawSharp.Channels;
using ClawSharp.Core.Channels;
using ClawSharp.Core.Config;
using ClawSharp.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

using TestConfig = ClawSharp.TestHelpers.TestHelpers;

namespace ClawSharp.Channels.Tests;

public class SlackChannelTests
{
    [Fact]
    public void Name_ReturnsSlack()
    {
        // Arrange
        var config = TestConfig.CreateTestConfig();
        config.Channels.Slack = new SlackChannelConfig 
        { 
            BotToken = "xoxb-test-token",
            AppToken = "xapp-test-token"
        };
        
        // Act
        var channel = new SlackChannel(config, NullLogger<SlackChannel>.Instance);
        
        // Assert
        channel.Name.Should().Be("slack");
    }

    [Fact]
    public void Constructor_WithoutSlackConfig_Throws()
    {
        // Arrange
        var config = TestConfig.CreateTestConfig();
        config.Channels.Slack = null;
        
        // Act
        var act = () => new SlackChannel(config, NullLogger<SlackChannel>.Instance);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Slack*");
    }

    [Fact]
    public void Constructor_WithoutBotToken_Throws()
    {
        // Arrange
        var config = TestConfig.CreateTestConfig();
        config.Channels.Slack = new SlackChannelConfig 
        { 
            BotToken = null,
            AppToken = "xapp-test-token"
        };
        
        // Act
        var act = () => new SlackChannel(config, NullLogger<SlackChannel>.Instance);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*BotToken*");
    }

    [Fact]
    public void Constructor_WithoutAppToken_Throws()
    {
        // Arrange
        var config = TestConfig.CreateTestConfig();
        config.Channels.Slack = new SlackChannelConfig 
        { 
            BotToken = "xoxb-test-token",
            AppToken = null
        };
        
        // Act
        var act = () => new SlackChannel(config, NullLogger<SlackChannel>.Instance);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AppToken*");
    }
}

public class SlackChannelMessageHandlingTests
{
    [Fact]
    public async Task HandleEvent_AppMention_TriggersOnMessage()
    {
        // Arrange
        var config = TestConfig.CreateTestConfig();
        config.Channels.Slack = new SlackChannelConfig 
        { 
            BotToken = "xoxb-test-token",
            AppToken = "xapp-test-token"
        };
        
        var channel = new SlackChannel(config, NullLogger<SlackChannel>.Instance);
        ChannelMessage? received = null;
        channel.OnMessage += msg => { received = msg; return Task.CompletedTask; };

        // Act - simulate an app mention
        await channel.SimulateAppMentionAsync("U123456", "Hello <@UBOT123> how are you?", "C123456");

        // Assert
        received.Should().NotBeNull();
        received!.Content.Should().Be("Hello  how are you?"); // Mention should be stripped
        received.Sender.Should().Be("U123456");
        received.Channel.Should().Be("slack");
        received.ChatId.Should().Be("C123456");
    }

    [Fact]
    public async Task HandleEvent_DirectMessage_TriggersOnMessage()
    {
        // Arrange
        var config = TestConfig.CreateTestConfig();
        config.Channels.Slack = new SlackChannelConfig 
        { 
            BotToken = "xoxb-test-token",
            AppToken = "xapp-test-token"
        };
        
        var channel = new SlackChannel(config, NullLogger<SlackChannel>.Instance);
        ChannelMessage? received = null;
        channel.OnMessage += msg => { received = msg; return Task.CompletedTask; };

        // Act - simulate a direct message
        await channel.SimulateDirectMessageAsync("U123456", "Hello in DM", "D123456");

        // Assert
        received.Should().NotBeNull();
        received!.Content.Should().Be("Hello in DM");
        received.Sender.Should().Be("U123456");
        received.Channel.Should().Be("slack");
    }

    [Fact]
    public async Task HandleEvent_AllowedChannel_ProcessesMessage()
    {
        // Arrange
        var config = TestConfig.CreateTestConfig();
        config.Channels.Slack = new SlackChannelConfig 
        { 
            BotToken = "xoxb-test-token",
            AppToken = "xapp-test-token",
            AllowedChannels = ["C123456", "C789012"]
        };
        
        var channel = new SlackChannel(config, NullLogger<SlackChannel>.Instance);
        ChannelMessage? received = null;
        channel.OnMessage += msg => { received = msg; return Task.CompletedTask; };

        // Act - message from allowed channel
        await channel.SimulateAppMentionAsync("U123456", "Hello", "C123456");

        // Assert
        received.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleEvent_NotAllowedChannel_IsIgnored()
    {
        // Arrange
        var config = TestConfig.CreateTestConfig();
        config.Channels.Slack = new SlackChannelConfig 
        { 
            BotToken = "xoxb-test-token",
            AppToken = "xapp-test-token",
            AllowedChannels = ["C123456"]
        };
        
        var channel = new SlackChannel(config, NullLogger<SlackChannel>.Instance);
        ChannelMessage? received = null;
        channel.OnMessage += msg => { received = msg; return Task.CompletedTask; };

        // Act - message from disallowed channel
        await channel.SimulateAppMentionAsync("U123456", "Hello", "C999999");

        // Assert
        received.Should().BeNull();
    }

    [Fact]
    public async Task HandleEvent_NoAllowlist_AllowsAllChannels()
    {
        // Arrange
        var config = TestConfig.CreateTestConfig();
        config.Channels.Slack = new SlackChannelConfig 
        { 
            BotToken = "xoxb-test-token",
            AppToken = "xapp-test-token",
            AllowedChannels = [] // Empty = allow all
        };
        
        var channel = new SlackChannel(config, NullLogger<SlackChannel>.Instance);
        ChannelMessage? received = null;
        channel.OnMessage += msg => { received = msg; return Task.CompletedTask; };

        // Act - message from any channel
        await channel.SimulateAppMentionAsync("U123456", "Hello", "C999999");

        // Assert
        received.Should().NotBeNull();
    }
}

public class SlackChannelSendingTests
{
    [Fact]
    public async Task SendAsync_FormatsAsMrkdwn()
    {
        // Arrange
        var config = TestConfig.CreateTestConfig();
        config.Channels.Slack = new SlackChannelConfig 
        { 
            BotToken = "xoxb-test-token",
            AppToken = "xapp-test-token"
        };
        
        var channel = new SlackChannel(config, NullLogger<SlackChannel>.Instance);
        await channel.StartAsync(CancellationToken.None);
        
        // Act & Assert - should work without throwing
        var outboundMessage = new OutboundMessage("C123456", "Hello *bold* _italic_ `code`");
        await channel.SendAsync(outboundMessage, CancellationToken.None);
    }

    [Fact]
    public async Task SendAsync_LongMessage_SplitsIntoChunks()
    {
        // Arrange
        var config = TestConfig.CreateTestConfig();
        config.Channels.Slack = new SlackChannelConfig 
        { 
            BotToken = "xoxb-test-token",
            AppToken = "xapp-test-token"
        };
        
        var channel = new SlackChannel(config, NullLogger<SlackChannel>.Instance);
        await channel.StartAsync(CancellationToken.None);
        
        // Act & Assert - long message should be handled without throwing
        var longMessage = new string('a', 35000);
        var outboundMessage = new OutboundMessage("C123456", longMessage);
        
        await channel.SendAsync(outboundMessage, CancellationToken.None);
    }

    [Fact]
    public async Task SendAsync_WithoutStart_Throws()
    {
        // Arrange
        var config = TestConfig.CreateTestConfig();
        config.Channels.Slack = new SlackChannelConfig 
        { 
            BotToken = "xoxb-test-token",
            AppToken = "xapp-test-token"
        };
        
        var channel = new SlackChannel(config, NullLogger<SlackChannel>.Instance);
        
        // Act
        var outboundMessage = new OutboundMessage("C123456", "Hello");
        var act = async () => await channel.SendAsync(outboundMessage, CancellationToken.None);
        
        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
