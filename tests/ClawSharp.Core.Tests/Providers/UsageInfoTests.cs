using System.Text.Json;
using ClawSharp.Core.Providers;
using FluentAssertions;

namespace ClawSharp.Core.Tests.Providers;

public class UsageInfoTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var usage = new UsageInfo(100, 50, 150);

        usage.PromptTokens.Should().Be(100);
        usage.CompletionTokens.Should().Be(50);
        usage.TotalTokens.Should().Be(150);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new UsageInfo(10, 20, 30);
        var b = new UsageInfo(10, 20, 30);
        a.Should().Be(b);
    }

    [Fact]
    public void JsonRoundtrip_PreservesValues()
    {
        var usage = new UsageInfo(5, 10, 15);
        var json = JsonSerializer.Serialize(usage);
        var deserialized = JsonSerializer.Deserialize<UsageInfo>(json);

        deserialized.Should().Be(usage);
    }

    [Fact]
    public void JsonSerialization_UsesCorrectPropertyNames()
    {
        var usage = new UsageInfo(1, 2, 3);
        var json = JsonSerializer.Serialize(usage);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("prompt_tokens").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("completion_tokens").GetInt32().Should().Be(2);
        doc.RootElement.GetProperty("total_tokens").GetInt32().Should().Be(3);
    }
}
