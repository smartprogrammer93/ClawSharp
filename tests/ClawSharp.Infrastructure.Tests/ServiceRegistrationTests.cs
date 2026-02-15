using ClawSharp.Core.Channels;
using ClawSharp.Core.Config;
using ClawSharp.Core.Security;
using ClawSharp.Core.Tools;
using ClawSharp.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ClawSharp.Infrastructure.Tests;

public class ServiceRegistrationTests
{
    private static ClawSharpConfig CreateTestConfig() => new()
    {
        WorkspaceDir = "/tmp/clawsharp-test",
        DataDir = "/tmp/clawsharp-test-data"
    };

    [Fact]
    public void AddClawSharp_RegistersConfig()
    {
        var services = new ServiceCollection();
        services.AddClawSharp(CreateTestConfig());
        var provider = services.BuildServiceProvider();

        provider.GetService<ClawSharpConfig>().Should().NotBeNull();
    }

    [Fact]
    public void AddClawSharp_RegistersMessageBus()
    {
        var services = new ServiceCollection();
        services.AddClawSharp(CreateTestConfig());
        var provider = services.BuildServiceProvider();

        var bus = provider.GetService<IMessageBus>();
        bus.Should().NotBeNull();

        // Verify singleton lifetime
        var bus2 = provider.GetService<IMessageBus>();
        bus.Should().BeSameAs(bus2);
    }

    [Fact]
    public void AddClawSharp_RegistersToolRegistry()
    {
        var services = new ServiceCollection();
        services.AddClawSharp(CreateTestConfig());
        var provider = services.BuildServiceProvider();

        var registry = provider.GetService<IToolRegistry>();
        registry.Should().NotBeNull();

        // Verify singleton lifetime
        var registry2 = provider.GetService<IToolRegistry>();
        registry.Should().BeSameAs(registry2);
    }

    [Fact]
    public void AddClawSharp_RegistersSecurityPolicy()
    {
        var services = new ServiceCollection();
        services.AddClawSharp(CreateTestConfig());
        var provider = services.BuildServiceProvider();

        provider.GetService<ISecurityPolicy>().Should().NotBeNull();
    }

    [Fact]
    public void AddClawSharp_RegistersLogging()
    {
        var services = new ServiceCollection();
        services.AddClawSharp(CreateTestConfig());
        var provider = services.BuildServiceProvider();

        provider.GetService<ILoggerFactory>().Should().NotBeNull();
        provider.GetService<ILogger<ServiceRegistrationTests>>().Should().NotBeNull();
    }
}
