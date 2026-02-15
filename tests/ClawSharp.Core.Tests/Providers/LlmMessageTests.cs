using System.Text.Json;
using ClawSharp.Core.Providers;
using FluentAssertions;

namespace ClawSharp.Core.Tests.Providers;

public class LlmMessageTests
{
    [Fact]
    public void Constructor_WithRequiredParams_SetsProperties()
    {
        var msg = new LlmMessage("user", "Hello");

        msg.Role.Should().Be("user");
        msg.Content.Should().Be("Hello");
        msg.ToolCalls.Should().BeNull();
        msg.ToolCallId.Should().BeNull();
        msg.Name.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAllParams_SetsProperties()
    {
        var toolCalls = new List<ToolCallRequest> { new("tc1", "read", "{}") };
        var msg = new LlmMessage("assistant", "Sure", toolCalls, "tc0", "bot");

        msg.Role.Should().Be("assistant");
        msg.Content.Should().Be("Sure");
        msg.ToolCalls.Should().HaveCount(1);
        msg.ToolCallId.Should().Be("tc0");
        msg.Name.Should().Be("bot");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new LlmMessage("user", "Hi");
        var b = new LlmMessage("user", "Hi");

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new LlmMessage("user", "Hi");
        var b = new LlmMessage("user", "Bye");

        a.Should().NotBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new LlmMessage("user", "Hi");
        var modified = original with { Content = "Bye" };

        modified.Content.Should().Be("Bye");
        original.Content.Should().Be("Hi");
    }

    [Fact]
    public void JsonRoundtrip_PreservesValues()
    {
        var msg = new LlmMessage("system", "You are helpful", Name: "system");
        var json = JsonSerializer.Serialize(msg);
        var deserialized = JsonSerializer.Deserialize<LlmMessage>(json);

        deserialized.Should().Be(msg);
    }

    [Fact]
    public void JsonSerialization_UsesCorrectPropertyNames()
    {
        var msg = new LlmMessage("user", "test");
        var json = JsonSerializer.Serialize(msg);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("role").GetString().Should().Be("user");
        doc.RootElement.GetProperty("content").GetString().Should().Be("test");
    }
}
