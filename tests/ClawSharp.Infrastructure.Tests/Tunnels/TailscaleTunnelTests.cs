using ClawSharp.Infrastructure.Tunnels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ClawSharp.Infrastructure.Tests.Tunnels;

public class TailscaleTunnelTests
{
    [Fact]
    public void Name_ReturnsTailscale()
    {
        var logger = Substitute.For<ILogger<TailscaleTunnel>>();
        var tunnel = new TailscaleTunnel(logger);
        tunnel.Name.Should().Be("tailscale");
    }

    [Fact]
    public void PublicUrl_BeforeStart_IsNull()
    {
        var logger = Substitute.For<ILogger<TailscaleTunnel>>();
        var tunnel = new TailscaleTunnel(logger);
        tunnel.PublicUrl.Should().BeNull();
    }

    [Fact]
    public void IsRunning_BeforeStart_ReturnsFalse()
    {
        var logger = Substitute.For<ILogger<TailscaleTunnel>>();
        var tunnel = new TailscaleTunnel(logger);
        tunnel.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_BeforeStart_DoesNotThrow()
    {
        var logger = Substitute.For<ILogger<TailscaleTunnel>>();
        var tunnel = new TailscaleTunnel(logger);
        
        var act = () => tunnel.StopAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Constructor_WithoutHostname_SetsNull()
    {
        var logger = Substitute.For<ILogger<TailscaleTunnel>>();
        var tunnel = new TailscaleTunnel(logger);
        
        tunnel.Hostname.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithHostname_StoresHostname()
    {
        var logger = Substitute.For<ILogger<TailscaleTunnel>>();
        var tunnel = new TailscaleTunnel(logger, hostname: "my-hostname");
        
        tunnel.Hostname.Should().Be("my-hostname");
    }
}
