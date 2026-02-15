using System.Text.Json;
using ClawSharp.Core.Channels;
using FluentAssertions;

namespace ClawSharp.Core.Tests.Channels;

public class ChannelMessageTests
{
    [Fact]
    public void Constructor_WithRequiredParams_SetsDefaults()
    {
        var ts = DateTimeOffset.UtcNow;
        var msg = new ChannelMessage("1", "user1", "Hello", "telegram", "chat1", ts);

        msg.Id.Should().Be("1");
        msg.Sender.Should().Be("user1");
        msg.Content.Should().Be("Hello");
        msg.Channel.Should().Be("telegram");
        msg.ChatId.Should().Be("chat1");
        msg.Timestamp.Should().Be(ts);
        msg.Media.Should().BeNull();
        msg.Metadata.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithOptionalParams()
    {
        var media = new List<string> { "/tmp/photo.jpg" };
        var metadata = new Dictionary<string, string> { ["key"] = "value" };
        var msg = new ChannelMessage("1", "u", "Hi", "discord", "c1", DateTimeOffset.UtcNow, media, metadata);

        msg.Media.Should().HaveCount(1);
        msg.Metadata.Should().ContainKey("key");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var ts = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var a = new ChannelMessage("1", "u", "Hi", "tg", "c", ts);
        var b = new ChannelMessage("1", "u", "Hi", "tg", "c", ts);
        a.Should().Be(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var msg = new ChannelMessage("1", "u", "Hi", "tg", "c", DateTimeOffset.UtcNow);
        var modified = msg with { Content = "Bye" };
        modified.Content.Should().Be("Bye");
        msg.Content.Should().Be("Hi");
    }
}
