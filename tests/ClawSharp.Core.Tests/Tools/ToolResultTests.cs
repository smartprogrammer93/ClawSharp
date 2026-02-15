using ClawSharp.Core.Tools;
using FluentAssertions;

namespace ClawSharp.Core.Tests.Tools;

public class ToolResultTests
{
    [Fact]
    public void Constructor_Success()
    {
        var result = new ToolResult(true, "output");

        result.Success.Should().BeTrue();
        result.Output.Should().Be("output");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Constructor_Failure()
    {
        var result = new ToolResult(false, "", "something went wrong");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("something went wrong");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new ToolResult(true, "ok");
        var b = new ToolResult(true, "ok");
        a.Should().Be(b);
    }
}
