using ClawSharp.Infrastructure.Tunnels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ClawSharp.Infrastructure.Tests.Tunnels;

public class CloudflareTunnelTests
{
    [Fact]
    public void Name_ReturnsCloudflare()
    {
        var logger = Substitute.For<ILogger<CloudflareTunnel>>();
        var tunnel = new CloudflareTunnel(logger);
        tunnel.Name.Should().Be("cloudflare");
    }

    [Fact]
    public void PublicUrl_BeforeStart_IsNull()
    {
        var logger = Substitute.For<ILogger<CloudflareTunnel>>();
        var tunnel = new CloudflareTunnel(logger);
        tunnel.PublicUrl.Should().BeNull();
    }

    [Fact]
    public void IsRunning_BeforeStart_ReturnsFalse()
    {
        var logger = Substitute.For<ILogger<CloudflareTunnel>>();
        var tunnel = new CloudflareTunnel(logger);
        tunnel.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_BeforeStart_DoesNotThrow()
    {
        var logger = Substitute.For<ILogger<CloudflareTunnel>>();
        var tunnel = new CloudflareTunnel(logger);
        
        var act = () => tunnel.StopAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Constructor_WithToken_StoresToken()
    {
        var logger = Substitute.For<ILogger<CloudflareTunnel>>();
        var tunnel = new CloudflareTunnel(logger, token: "my-token");
        
        tunnel.Token.Should().Be("my-token");
    }

    [Fact]
    public void Constructor_WithDomain_StoresDomain()
    {
        var logger = Substitute.For<ILogger<CloudflareTunnel>>();
        var tunnel = new CloudflareTunnel(logger, domain: "example.com");
        
        tunnel.Domain.Should().Be("example.com");
    }

    [Fact]
    public void Constructor_WithoutOptionalParams_SetsNulls()
    {
        var logger = Substitute.For<ILogger<CloudflareTunnel>>();
        var tunnel = new CloudflareTunnel(logger);
        
        tunnel.Token.Should().BeNull();
        tunnel.Domain.Should().BeNull();
    }
}
