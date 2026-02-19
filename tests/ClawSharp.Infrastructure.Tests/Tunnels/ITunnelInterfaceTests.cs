using ClawSharp.Infrastructure.Tunnels;
using FluentAssertions;
using NSubstitute;

namespace ClawSharp.Infrastructure.Tests.Tunnels;

public class ITunnelInterfaceTests
{
    [Fact]
    public void ITunnel_HasRequiredMembers()
    {
        // Verify ITunnel interface defines required members
        var tunnel = Substitute.For<ITunnel>();
        
        // Name property
        tunnel.Name.Returns("test-tunnel");
        tunnel.Name.Should().Be("test-tunnel");
        
        // PublicUrl property
        tunnel.PublicUrl.Returns("https://test.example.com");
        tunnel.PublicUrl.Should().Be("https://test.example.com");
        
        // IsRunning property
        tunnel.IsRunning.Returns(true);
        tunnel.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void ITunnel_StartAsync_IsDefined()
    {
        var tunnel = Substitute.For<ITunnel>();
        
        // Verify StartAsync method exists with correct signature
        tunnel.Received(0).StartAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ITunnel_StopAsync_IsDefined()
    {
        var tunnel = Substitute.For<ITunnel>();
        
        // Verify StopAsync method exists
        tunnel.Received(0).StopAsync();
    }
}
