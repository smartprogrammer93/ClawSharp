using Bunit;
using ClawSharp.UI.Pages;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace ClawSharp.UI.Tests;

public class SettingsPageTests : TestContext
{
    public SettingsPageTests()
    {
        Services.AddMudServices();
        Services.AddMudPopoverService();
        Services.AddScoped<HttpClient>(_ => new HttpClient { BaseAddress = new Uri("http://localhost") });
        
        // Setup MudBlazor JS interop mocks
        JSInterop.Mode = JSRuntimeMode.Loose;
        
        // Add MudPopoverProvider to the render tree
    }

    [Fact]
    public void Settings_Renders()
    {
        // Arrange & Act
        var cut = RenderComponent<Settings>();

        // Assert
        cut.Markup.Should().Contain("Settings");
    }

    [Fact]
    public void Settings_ShowsGeneralSettingsSection()
    {
        // Arrange & Act
        var cut = RenderComponent<Settings>();

        // Assert
        cut.Markup.Should().Contain("General Settings");
    }

    [Fact]
    public void Settings_ShowsProviderSection()
    {
        // Arrange & Act
        var cut = RenderComponent<Settings>();

        // Assert
        cut.Markup.Should().Contain("Provider API Keys");
    }

    [Fact]
    public void Settings_ShowsChannelsSection()
    {
        // Arrange & Act
        var cut = RenderComponent<Settings>();

        // Assert
        cut.Markup.Should().Contain("Channels");
    }

    [Fact]
    public void Settings_HasDataDirectoryField()
    {
        // Arrange & Act
        var cut = RenderComponent<Settings>();

        // Assert
        cut.Markup.Should().Contain("Data Directory");
    }

    [Fact]
    public void Settings_HasWorkspaceDirectoryField()
    {
        // Arrange & Act
        var cut = RenderComponent<Settings>();

        // Assert
        cut.Markup.Should().Contain("Workspace Directory");
    }

    [Fact]
    public void Settings_HasDefaultProviderField()
    {
        // Arrange & Act
        var cut = RenderComponent<Settings>();

        // Assert
        cut.Markup.Should().Contain("Default Provider");
    }
}
