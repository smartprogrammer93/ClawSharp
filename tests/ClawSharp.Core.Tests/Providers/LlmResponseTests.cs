using System.Text.Json;
using ClawSharp.Core.Providers;
using FluentAssertions;

namespace ClawSharp.Core.Tests.Providers;

public class LlmResponseTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var usage = new UsageInfo(10, 20, 30);
        var toolCalls = new List<ToolCallRequest>();
        var response = new LlmResponse("Hello!", toolCalls, "stop", usage);

        response.Content.Should().Be("Hello!");
        response.ToolCalls.Should().BeEmpty();
        response.FinishReason.Should().Be("stop");
        response.Usage.Should().Be(usage);
    }

    [Fact]
    public void Constructor_WithNullUsage()
    {
        var response = new LlmResponse("Hi", [], "stop", null);
        response.Usage.Should().BeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new LlmResponse("Hi", [], "stop", new UsageInfo(1, 2, 3));
        var b = new LlmResponse("Hi", [], "stop", new UsageInfo(1, 2, 3));
        a.Should().Be(b);
    }

    [Fact]
    public void JsonRoundtrip_PreservesValues()
    {
        var response = new LlmResponse("Hello", [], "stop", new UsageInfo(10, 5, 15));
        var json = JsonSerializer.Serialize(response);
        var deserialized = JsonSerializer.Deserialize<LlmResponse>(json);

        deserialized!.Content.Should().Be("Hello");
        deserialized.FinishReason.Should().Be("stop");
        deserialized.Usage!.PromptTokens.Should().Be(10);
    }

    [Fact]
    public void JsonSerialization_UsesCorrectPropertyNames()
    {
        var response = new LlmResponse("test", [], "length", null);
        var json = JsonSerializer.Serialize(response);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("content").GetString().Should().Be("test");
        doc.RootElement.GetProperty("finish_reason").GetString().Should().Be("length");
        doc.RootElement.GetProperty("tool_calls").GetArrayLength().Should().Be(0);
    }
}
