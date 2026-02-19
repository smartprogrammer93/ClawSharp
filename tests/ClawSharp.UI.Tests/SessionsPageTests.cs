using Bunit;
using ClawSharp.UI.Pages;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace ClawSharp.UI.Tests;

public class SessionsPageTests : TestContext
{
    public SessionsPageTests()
    {
        Services.AddMudServices();
        Services.AddMudPopoverService();
        Services.AddScoped<HttpClient>(_ => new HttpClient { BaseAddress = new Uri("http://localhost") });
        
        // Setup MudBlazor JS interop mocks
        JSInterop.Mode = JSRuntimeMode.Loose;
        
        // Add MudPopoverProvider to the render tree
    }

    [Fact]
    public void Sessions_Renders()
    {
        // Arrange & Act
        var cut = RenderComponent<Sessions>();

        // Assert
        cut.Markup.Should().Contain("Sessions");
    }

    [Fact]
    public void Sessions_HasRefreshButton()
    {
        // Arrange & Act
        var cut = RenderComponent<Sessions>();

        // Assert
        cut.Markup.Should().Contain("Refresh");
    }

    [Fact]
    public void Sessions_HasSessionKeyColumn()
    {
        // Arrange & Act
        var cut = RenderComponent<Sessions>();

        // Assert
        cut.Markup.Should().Contain("Session Key");
    }

    [Fact]
    public void Sessions_HasChannelColumn()
    {
        // Arrange & Act
        var cut = RenderComponent<Sessions>();

        // Assert
        cut.Markup.Should().Contain("Channel");
    }

    [Fact]
    public void Sessions_HasChatIdColumn()
    {
        // Arrange & Act
        var cut = RenderComponent<Sessions>();

        // Assert
        cut.Markup.Should().Contain("Chat ID");
    }
}
