namespace ClawSharp.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void Assembly_ShouldLoad() => Assert.NotNull(typeof(ClawSharp.Core.AssemblyMarker));
}
