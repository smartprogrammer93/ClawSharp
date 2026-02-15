using ClawSharp.Agent;
using ClawSharp.Core.Channels;
using ClawSharp.Core.Config;
using ClawSharp.Core.Providers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net.Http.Json;

namespace ClawSharp.Gateway.Tests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"ok\"");
    }
}

public class AgentEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AgentEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostAgent_WithValidRequest_ReturnsResponse()
    {
        // Arrange
        var request = new { message = "Hello", sessionKey = "test-session" };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/agent", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("response");
    }

    [Fact]
    public async Task PostAgent_WithoutSessionKey_CreatesNewSession()
    {
        // Arrange
        var request = new { message = "Hello" };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/agent", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostAgent_WithEmptyMessage_ReturnsBadRequest()
    {
        // Arrange
        var request = new { message = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/agent", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }
}

public class SessionsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SessionsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetSessions_ReturnsOk()
    {
        var response = await _client.GetAsync("/v1/sessions");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSessions_ReturnsSessionList()
    {
        var response = await _client.GetAsync("/v1/sessions");
        var body = await response.Content.ReadAsStringAsync();
        body.Trim().Should().Be("[]");
    }
}

public class MessagesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MessagesEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostMessages_WithValidRequest_ReturnsAccepted()
    {
        // Arrange
        var request = new { channel = "test", chatId = "123", content = "Hello" };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/messages", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task PostMessages_WithMissingContent_ReturnsBadRequest()
    {
        // Arrange
        var request = new { channel = "test", chatId = "123" };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/messages", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }
}

public class GatewayConfigurationTests
{
    [Fact]
    public void Gateway_ConfiguresOnPort8080ByDefault()
    {
        // This test verifies that the gateway is configured to run on port 8080 by default
        var config = new ClawSharpConfig();
        config.Gateway.Port.Should().Be(8080);
    }
}
