using ClawSharp.Core.Memory;
using FluentAssertions;

namespace ClawSharp.Core.Tests.Memory;

public class MemoryEntryTests
{
    [Fact]
    public void Constructor_WithRequiredParams_SetsDefaults()
    {
        var ts = DateTimeOffset.UtcNow;
        var entry = new MemoryEntry("id1", "key1", "content", MemoryCategory.Core, ts);

        entry.Id.Should().Be("id1");
        entry.Key.Should().Be("key1");
        entry.Content.Should().Be("content");
        entry.Category.Should().Be(MemoryCategory.Core);
        entry.Timestamp.Should().Be(ts);
        entry.SessionId.Should().BeNull();
        entry.Score.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithOptionalParams()
    {
        var entry = new MemoryEntry("id", "k", "c", MemoryCategory.Daily, DateTimeOffset.UtcNow, "sess1", 0.95);

        entry.SessionId.Should().Be("sess1");
        entry.Score.Should().Be(0.95);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var ts = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var a = new MemoryEntry("1", "k", "c", MemoryCategory.Core, ts);
        var b = new MemoryEntry("1", "k", "c", MemoryCategory.Core, ts);
        a.Should().Be(b);
    }
}

public class MemoryCategoryTests
{
    [Fact]
    public void AllValues_AreDefined()
    {
        Enum.GetValues<MemoryCategory>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(MemoryCategory.Core, 0)]
    [InlineData(MemoryCategory.Daily, 1)]
    [InlineData(MemoryCategory.Conversation, 2)]
    [InlineData(MemoryCategory.Custom, 3)]
    public void Values_HaveExpectedOrdinals(MemoryCategory category, int expected)
    {
        ((int)category).Should().Be(expected);
    }
}
