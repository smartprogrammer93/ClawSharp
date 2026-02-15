namespace ClawSharp.Agent.Tests;

public class SmokeTests
{
    [Fact]
    public void Assembly_ShouldLoad() => Assert.NotNull(typeof(ClawSharp.Agent.AssemblyMarker));
}
