using System.Text.Json;
using ClawSharp.Core.Providers;
using FluentAssertions;

namespace ClawSharp.Core.Tests.Providers;

public class ToolSpecTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var schema = JsonDocument.Parse("""{"type": "object"}""").RootElement;
        var spec = new ToolSpec("read", "Read a file", schema);

        spec.Name.Should().Be("read");
        spec.Description.Should().Be("Read a file");
        spec.ParametersSchema.GetProperty("type").GetString().Should().Be("object");
    }

    [Fact]
    public void JsonRoundtrip_PreservesValues()
    {
        var schema = JsonDocument.Parse("{}").RootElement;
        var spec = new ToolSpec("exec", "Execute command", schema);
        var json = JsonSerializer.Serialize(spec);
        var deserialized = JsonSerializer.Deserialize<ToolSpec>(json);

        deserialized!.Name.Should().Be("exec");
        deserialized.Description.Should().Be("Execute command");
    }
}
