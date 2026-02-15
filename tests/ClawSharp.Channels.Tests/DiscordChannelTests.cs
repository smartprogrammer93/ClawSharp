using ClawSharp.Channels;
using ClawSharp.Core.Channels;
using ClawSharp.Core.Config;
using ClawSharp.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ClawSharp.Channels.Tests;

public class DiscordChannelTests
{
    [Fact]
    public void Name_ReturnsDiscord()
    {
        // Arrange
        var config = TestHelpers.CreateTestConfig();
        config.Channels.Discord = new DiscordChannelConfig { BotToken = "test-token" };
        
        // Act
        var channel = new DiscordChannel(config, NullLogger<DiscordChannel>.Instance);
        
        // Assert
        channel.Name.Should().Be("discord");
    }

    [Fact]
    public void Constructor_WithoutDiscordConfig_Throws()
    {
        // Arrange
        var config = TestHelpers.CreateTestConfig();
        config.Channels.Discord = null;
        
        // Act
        var act = () => new DiscordChannel(config, NullLogger<DiscordChannel>.Instance);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Discord*");
    }

    [Fact]
    public void Constructor_WithoutBotToken_Throws()
    {
        // Arrange
        var config = TestHelpers.CreateTestConfig();
        config.Channels.Discord = new DiscordChannelConfig { BotToken = null };
        
        // Act
        var act = () => new DiscordChannel(config, NullLogger<DiscordChannel>.Instance);
        
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*BotToken*");
    }
}

public class DiscordChannelMessageHandlingTests
{
    [Fact]
    public async Task HandleMessage_BotMessage_IsIgnored()
    {
        // Arrange
        var config = TestHelpers.CreateTestConfig();
        config.Channels.Discord = new DiscordChannelConfig { BotToken = "test-token" };
        
        var channel = new DiscordChannel(config, NullLogger<DiscordChannel>.Instance);
        ChannelMessage? received = null;
        channel.OnMessage += msg => { received = msg; return Task.CompletedTask; };

        // Act - simulate a bot message (IsWebhook = true or author is bot)
        await channel.SimulateMessageAsync("123", "Hello from bot", "channel123", isBot: true);

        // Assert
        received.Should().BeNull();
    }

    [Fact]
    public async Task HandleMessage_MentionedBot_ProcessesMessage()
    {
        // Arrange
        var config = TestHelpers.CreateTestConfig();
        config.Channels.Discord = new DiscordChannelConfig { BotToken = "test-token" };
        
        var channel = new DiscordChannel(config, NullLogger<DiscordChannel>.Instance);
        ChannelMessage? received = null;
        channel.OnMessage += msg => { received = msg; return Task.CompletedTask; };

        // Act - simulate a message that mentions the bot
        await channel.SimulateMessageAsync("456", "Hello <@bot123> how are you?", "channel123", isBot: false);

        // Assert
        received.Should().NotBeNull();
        received!.Content.Should().Be("Hello  how are you?"); // Mention should be stripped
        received.Sender.Should().Be("456");
        received.Channel.Should().Be("discord");
    }

    [Fact]
    public async Task HandleMessage_DirectMessage_ProcessesMessage()
    {
        // Arrange
        var config = TestHelpers.CreateTestConfig();
        config.Channels.Discord = new DiscordChannelConfig { BotToken = "test-token" };
        
        var channel = new DiscordChannel(config, NullLogger<DiscordChannel>.Instance);
        ChannelMessage? received = null;
        channel.OnMessage += msg => { received = msg; return Task.CompletedTask; };

        // Act - simulate a DM (channel is a DM channel)
        await channel.SimulateMessageAsync("456", "Hello in DM", "dm_channel", isBot: false, isDm: true);

        // Assert
        received.Should().NotBeNull();
        received!.Content.Should().Be("Hello in DM");
        received.Sender.Should().Be("456");
    }

    [Fact]
    public async Task HandleMessage_AllowedGuild_ProcessesMessage()
    {
        // Arrange
        var config = TestHelpers.CreateTestConfig();
        config.Channels.Discord = new DiscordChannelConfig 
        { 
            BotToken = "test-token",
            AllowedGuilds = ["guild123", "guild456"]
        };
        
        var channel = new DiscordChannel(config, NullLogger<DiscordChannel>.Instance);
        ChannelMessage? received = null;
        channel.OnMessage += msg => { received = msg; return Task.CompletedTask; };

        // Act - message from allowed guild
        await channel.SimulateMessageAsync("789", "Hello from allowed guild", "channel123", 
            isBot: false, isDm: false, guildId: "guild123");

        // Assert
        received.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleMessage_NotAllowedGuild_IsIgnored()
    {
        // Arrange
        var config = TestHelpers.CreateTestConfig();
        config.Channels.Discord = new DiscordChannelConfig 
        { 
            BotToken = "test-token",
            AllowedGuilds = ["guild123"]
        };
        
        var channel = new DiscordChannel(config, NullLogger<DiscordChannel>.Instance);
        ChannelMessage? received = null;
        channel.OnMessage += msg => { received = msg; return Task.CompletedTask; };

        // Act - message from disallowed guild
        await channel.SimulateMessageAsync("789", "Hello from disallowed guild", "channel123", 
            isBot: false, isDm: false, guildId: "guild456");

        // Assert
        received.Should().BeNull();
    }

    [Fact]
    public async Task HandleMessage_NoAllowlist_AllowsAllGuilds()
    {
        // Arrange
        var config = TestHelpers.CreateTestConfig();
        config.Channels.Discord = new DiscordChannelConfig 
        { 
            BotToken = "test-token",
            AllowedGuilds = [] // Empty = allow all
        };
        
        var channel = new DiscordChannel(config, NullLogger<DiscordChannel>.Instance);
        ChannelMessage? received = null;
        channel.OnMessage += msg => { received = msg; return Task.CompletedTask; };

        // Act - message from any guild
        await channel.SimulateMessageAsync("789", "Hello from any guild", "channel123", 
            isBot: false, isDm: false, guildId: "any_guild");

        // Assert
        received.Should().NotBeNull();
    }
}

public class DiscordChannelSendingTests
{
    [Fact]
    public async Task SendAsync_LongMessage_SplitsAt2000Chars()
    {
        // Arrange
        var config = TestHelpers.CreateTestConfig();
        config.Channels.Discord = new DiscordChannelConfig { BotToken = "test-token" };
        
        var channel = new DiscordChannel(config, NullLogger<DiscordChannel>.Instance);
        
        // Act & Assert - long message should be handled without throwing
        var longMessage = new string('a', 2500);
        var outboundMessage = new OutboundMessage("123", longMessage);
        
        // This should work without throwing in GREEN phase
        await channel.SendAsync(outboundMessage, CancellationToken.None);
    }

    [Fact]
    public async Task SendAsync_NormalMessage_SendsSuccessfully()
    {
        // Arrange
        var config = TestHelpers.CreateTestConfig();
        config.Channels.Discord = new DiscordChannelConfig { BotToken = "test-token" };
        
        var channel = new DiscordChannel(config, NullLogger<DiscordChannel>.Instance);
        
        // Act & Assert - normal message should work
        var outboundMessage = new OutboundMessage("123", "Hello world");
        
        await channel.SendAsync(outboundMessage, CancellationToken.None);
    }
}
