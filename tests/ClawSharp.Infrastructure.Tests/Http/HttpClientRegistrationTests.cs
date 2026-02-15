using ClawSharp.Core.Config;
using ClawSharp.Infrastructure.Http;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TestConfig = ClawSharp.TestHelpers.TestHelpers;

namespace ClawSharp.Infrastructure.Tests.Http;

public class HttpClientRegistrationTests
{
    [Fact]
    public void AddProviderHttpClients_RegistersOpenAiClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TestConfig.CreateTestConfig());
        services.AddProviderHttpClients();
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("openai");
        client.Should().NotBeNull();
        client.DefaultRequestHeaders.UserAgent.ToString().Should().Contain("ClawSharp");
    }

    [Fact]
    public void AddProviderHttpClients_RegistersAnthropicClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TestConfig.CreateTestConfig());
        services.AddProviderHttpClients();
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("anthropic");
        client.Should().NotBeNull();
    }

    [Fact]
    public void AddProviderHttpClients_SetsCorrectTimeout()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TestConfig.CreateTestConfig());
        services.AddProviderHttpClients();
        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("openai");
        client.Timeout.Should().Be(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void AddProviderHttpClients_RegistersOpenRouterClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TestConfig.CreateTestConfig());
        services.AddProviderHttpClients();
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("openrouter");
        client.Should().NotBeNull();
    }

    [Fact]
    public void AddProviderHttpClients_RegistersOllamaClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TestConfig.CreateTestConfig());
        services.AddProviderHttpClients();
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("ollama");
        client.Should().NotBeNull();
    }
}
