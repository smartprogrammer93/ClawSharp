using ClawSharp.Core.Providers;
using ClawSharp.Core.Sessions;
using FluentAssertions;

namespace ClawSharp.Core.Tests.Sessions;

public class SessionContextTests
{
    [Fact]
    public void Constructor_SetsRequiredAndDefaults()
    {
        var ctx = new SessionContext
        {
            SessionKey = "key1",
            Channel = "telegram",
            ChatId = "chat1"
        };

        ctx.SessionKey.Should().Be("key1");
        ctx.Channel.Should().Be("telegram");
        ctx.ChatId.Should().Be("chat1");
        ctx.History.Should().BeEmpty();
        ctx.Summary.Should().BeNull();
        ctx.Created.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        ctx.LastActive.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void History_IsMutable()
    {
        var ctx = new SessionContext { SessionKey = "k", Channel = "c", ChatId = "ch" };
        ctx.History.Add(new LlmMessage("user", "Hi"));

        ctx.History.Should().HaveCount(1);
    }

    [Fact]
    public void LastActive_IsMutable()
    {
        var ctx = new SessionContext { SessionKey = "k", Channel = "c", ChatId = "ch" };
        var newTime = DateTimeOffset.Parse("2025-01-01T00:00:00Z");
        ctx.LastActive = newTime;

        ctx.LastActive.Should().Be(newTime);
    }

    [Fact]
    public void Summary_IsMutable()
    {
        var ctx = new SessionContext { SessionKey = "k", Channel = "c", ChatId = "ch" };
        ctx.Summary = "test summary";

        ctx.Summary.Should().Be("test summary");
    }
}
