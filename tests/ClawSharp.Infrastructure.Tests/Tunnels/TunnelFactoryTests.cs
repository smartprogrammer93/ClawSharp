using ClawSharp.Infrastructure.Tunnels;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ClawSharp.Infrastructure.Tests.Tunnels;

public class TunnelFactoryTests
{
    [Fact]
    public void SupportedProviders_ContainsCloudflare()
    {
        TunnelFactory.SupportedProviders.Should().Contain("cloudflare");
    }

    [Fact]
    public void SupportedProviders_ContainsNgrok()
    {
        TunnelFactory.SupportedProviders.Should().Contain("ngrok");
    }

    [Fact]
    public void SupportedProviders_ContainsTailscale()
    {
        TunnelFactory.SupportedProviders.Should().Contain("tailscale");
    }

    [Fact]
    public void Create_Cloudflare_ReturnsCloudflareTunnel()
    {
        var factory = new TunnelFactory(NullLoggerFactory.Instance);
        var tunnel = factory.Create("cloudflare");
        
        tunnel.Should().BeOfType<CloudflareTunnel>();
        tunnel.Name.Should().Be("cloudflare");
    }

    [Fact]
    public void Create_Ngrok_ReturnsNgrokTunnel()
    {
        var factory = new TunnelFactory(NullLoggerFactory.Instance);
        var tunnel = factory.Create("ngrok");
        
        tunnel.Should().BeOfType<NgrokTunnel>();
        tunnel.Name.Should().Be("ngrok");
    }

    [Fact]
    public void Create_Tailscale_ReturnsTailscaleTunnel()
    {
        var factory = new TunnelFactory(NullLoggerFactory.Instance);
        var tunnel = factory.Create("tailscale");
        
        tunnel.Should().BeOfType<TailscaleTunnel>();
        tunnel.Name.Should().Be("tailscale");
    }

    [Fact]
    public void Create_CaseInsensitive_Works()
    {
        var factory = new TunnelFactory(NullLoggerFactory.Instance);
        
        factory.Create("CLOUDFLARE").Name.Should().Be("cloudflare");
        factory.Create("NGROK").Name.Should().Be("ngrok");
        factory.Create("TaIlScAlE").Name.Should().Be("tailscale");
    }

    [Fact]
    public void Create_UnknownProvider_Throws()
    {
        var factory = new TunnelFactory(NullLoggerFactory.Instance);
        
        var act = () => factory.Create("unknown");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unknown tunnel provider*");
    }

    [Fact]
    public void Create_WithToken_PassesToCloudflare()
    {
        var factory = new TunnelFactory(NullLoggerFactory.Instance);
        var tunnel = (CloudflareTunnel)factory.Create("cloudflare", token: "my-token");
        
        tunnel.Token.Should().Be("my-token");
    }

    [Fact]
    public void Create_WithDomain_PassesToCloudflare()
    {
        var factory = new TunnelFactory(NullLoggerFactory.Instance);
        var tunnel = (CloudflareTunnel)factory.Create("cloudflare", domain: "example.com");
        
        tunnel.Domain.Should().Be("example.com");
    }

    [Fact]
    public void Create_WithToken_PassesToNgrok()
    {
        var factory = new TunnelFactory(NullLoggerFactory.Instance);
        var tunnel = (NgrokTunnel)factory.Create("ngrok", token: "ngrok-token");
        
        tunnel.AuthToken.Should().Be("ngrok-token");
    }

    [Fact]
    public void Create_WithDomain_PassesToTailscale()
    {
        var factory = new TunnelFactory(NullLoggerFactory.Instance);
        var tunnel = (TailscaleTunnel)factory.Create("tailscale", domain: "my-hostname");
        
        tunnel.Hostname.Should().Be("my-hostname");
    }
}
