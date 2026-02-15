using System.Text.Json;
using ClawSharp.Core.Providers;
using FluentAssertions;

namespace ClawSharp.Core.Tests.Providers;

public class LlmRequestTests
{
    [Fact]
    public void Constructor_WithRequiredParams_SetsDefaults()
    {
        var request = new LlmRequest
        {
            Model = "gpt-4o",
            Messages = [new LlmMessage("user", "Hi")]
        };

        request.Model.Should().Be("gpt-4o");
        request.Messages.Should().HaveCount(1);
        request.Tools.Should().BeNull();
        request.Temperature.Should().Be(0.7);
        request.MaxTokens.Should().BeNull();
        request.SystemPrompt.Should().BeNull();
        request.StopSequence.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAllParams_SetsProperties()
    {
        var schema = JsonDocument.Parse("{}").RootElement;
        var tools = new List<ClawSharp.Core.Providers.ToolSpec>
        {
            new("read", "Read a file", schema)
        };

        var request = new LlmRequest
        {
            Model = "claude-sonnet-4-20250514",
            Messages = [new LlmMessage("user", "Hi")],
            Tools = tools,
            Temperature = 0.5,
            MaxTokens = 1000,
            SystemPrompt = "Be helpful",
            StopSequence = "END"
        };

        request.Temperature.Should().Be(0.5);
        request.MaxTokens.Should().Be(1000);
        request.SystemPrompt.Should().Be("Be helpful");
        request.StopSequence.Should().Be("END");
        request.Tools.Should().HaveCount(1);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var request = new LlmRequest
        {
            Model = "gpt-4o",
            Messages = [new LlmMessage("user", "Hi")]
        };

        var modified = request with { Temperature = 1.0 };

        modified.Temperature.Should().Be(1.0);
        request.Temperature.Should().Be(0.7);
    }

    [Fact]
    public void JsonRoundtrip_PreservesValues()
    {
        var request = new LlmRequest
        {
            Model = "gpt-4o",
            Messages = [new LlmMessage("user", "Hello")],
            Temperature = 0.9,
            MaxTokens = 500
        };

        var json = JsonSerializer.Serialize(request);
        var deserialized = JsonSerializer.Deserialize<LlmRequest>(json);

        deserialized!.Model.Should().Be("gpt-4o");
        deserialized.Temperature.Should().Be(0.9);
        deserialized.MaxTokens.Should().Be(500);
        deserialized.Messages.Should().HaveCount(1);
    }

    [Fact]
    public void JsonSerialization_UsesSnakeCasePropertyNames()
    {
        var request = new LlmRequest
        {
            Model = "gpt-4o",
            Messages = [new LlmMessage("user", "Hi")],
            MaxTokens = 100,
            SystemPrompt = "test"
        };

        var json = JsonSerializer.Serialize(request);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("model").GetString().Should().Be("gpt-4o");
        doc.RootElement.GetProperty("max_tokens").GetInt32().Should().Be(100);
        doc.RootElement.GetProperty("system_prompt").GetString().Should().Be("test");
    }
}
