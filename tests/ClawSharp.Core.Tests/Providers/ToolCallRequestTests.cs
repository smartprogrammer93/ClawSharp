using System.Text.Json;
using ClawSharp.Core.Providers;
using FluentAssertions;

namespace ClawSharp.Core.Tests.Providers;

public class ToolCallRequestTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var tc = new ToolCallRequest("call-1", "read_file", """{"path": "/tmp"}""");

        tc.Id.Should().Be("call-1");
        tc.Name.Should().Be("read_file");
        tc.ArgumentsJson.Should().Contain("path");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new ToolCallRequest("1", "read", "{}");
        var b = new ToolCallRequest("1", "read", "{}");
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new ToolCallRequest("1", "read", "{}");
        var b = new ToolCallRequest("2", "read", "{}");
        a.Should().NotBe(b);
    }

    [Fact]
    public void JsonRoundtrip_PreservesValues()
    {
        var tc = new ToolCallRequest("tc-42", "exec", """{"cmd": "ls"}""");
        var json = JsonSerializer.Serialize(tc);
        var deserialized = JsonSerializer.Deserialize<ToolCallRequest>(json);

        deserialized.Should().Be(tc);
    }

    [Fact]
    public void JsonSerialization_UsesCorrectPropertyNames()
    {
        var tc = new ToolCallRequest("id1", "tool", "{}");
        var json = JsonSerializer.Serialize(tc);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("id").GetString().Should().Be("id1");
        doc.RootElement.GetProperty("name").GetString().Should().Be("tool");
        doc.RootElement.GetProperty("arguments_json").GetString().Should().Be("{}");
    }
}
