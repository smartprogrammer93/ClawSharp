using System.Text.Json;
using ClawSharp.Core.Providers;
using FluentAssertions;

namespace ClawSharp.Core.Tests.Providers;

public class LlmStreamChunkTests
{
    [Fact]
    public void Constructor_ContentDeltaOnly()
    {
        var chunk = new LlmStreamChunk("Hello", null, null, null);

        chunk.ContentDelta.Should().Be("Hello");
        chunk.ToolCallDelta.Should().BeNull();
        chunk.FinishReason.Should().BeNull();
        chunk.Usage.Should().BeNull();
    }

    [Fact]
    public void Constructor_FinalChunk_WithUsage()
    {
        var usage = new UsageInfo(10, 20, 30);
        var chunk = new LlmStreamChunk(null, null, "stop", usage);

        chunk.ContentDelta.Should().BeNull();
        chunk.FinishReason.Should().Be("stop");
        chunk.Usage.Should().Be(usage);
    }

    [Fact]
    public void JsonRoundtrip_PreservesValues()
    {
        var chunk = new LlmStreamChunk("delta", null, null, null);
        var json = JsonSerializer.Serialize(chunk);
        var deserialized = JsonSerializer.Deserialize<LlmStreamChunk>(json);

        deserialized.Should().Be(chunk);
    }
}
