using ClawSharp.Infrastructure.Tunnels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ClawSharp.Infrastructure.Tests.Tunnels;

public class NgrokTunnelTests
{
    [Fact]
    public void Name_ReturnsNgrok()
    {
        var logger = Substitute.For<ILogger<NgrokTunnel>>();
        var tunnel = new NgrokTunnel(logger);
        tunnel.Name.Should().Be("ngrok");
    }

    [Fact]
    public void PublicUrl_BeforeStart_IsNull()
    {
        var logger = Substitute.For<ILogger<NgrokTunnel>>();
        var tunnel = new NgrokTunnel(logger);
        tunnel.PublicUrl.Should().BeNull();
    }

    [Fact]
    public void IsRunning_BeforeStart_ReturnsFalse()
    {
        var logger = Substitute.For<ILogger<NgrokTunnel>>();
        var tunnel = new NgrokTunnel(logger);
        tunnel.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_BeforeStart_DoesNotThrow()
    {
        var logger = Substitute.For<ILogger<NgrokTunnel>>();
        var tunnel = new NgrokTunnel(logger);
        
        var act = () => tunnel.StopAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Constructor_WithAuthToken_StoresAuthToken()
    {
        var logger = Substitute.For<ILogger<NgrokTunnel>>();
        var tunnel = new NgrokTunnel(logger, authToken: "my-ngrok-token");
        
        tunnel.AuthToken.Should().Be("my-ngrok-token");
    }

    [Fact]
    public void Constructor_WithoutAuthToken_SetsNull()
    {
        var logger = Substitute.For<ILogger<NgrokTunnel>>();
        var tunnel = new NgrokTunnel(logger);
        
        tunnel.AuthToken.Should().BeNull();
    }
}
