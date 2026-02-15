using ClawSharp.Core.Channels;
using FluentAssertions;

namespace ClawSharp.Core.Tests.Channels;

public class OutboundMessageTests
{
    [Fact]
    public void Constructor_WithRequiredParams_SetsDefaults()
    {
        var msg = new OutboundMessage("chat1", "Hello");

        msg.ChatId.Should().Be("chat1");
        msg.Content.Should().Be("Hello");
        msg.FilePath.Should().BeNull();
        msg.ReplyToId.Should().BeNull();
        msg.Silent.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithAllParams()
    {
        var msg = new OutboundMessage("chat1", "Hi", "/tmp/f.txt", "msg1", true);

        msg.FilePath.Should().Be("/tmp/f.txt");
        msg.ReplyToId.Should().Be("msg1");
        msg.Silent.Should().BeTrue();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new OutboundMessage("c", "Hi");
        var b = new OutboundMessage("c", "Hi");
        a.Should().Be(b);
    }
}
