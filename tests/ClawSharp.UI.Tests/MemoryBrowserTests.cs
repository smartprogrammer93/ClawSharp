using Bunit;
using ClawSharp.UI.Pages;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace ClawSharp.UI.Tests;

public class MemoryBrowserTests : TestContext
{
    public MemoryBrowserTests()
    {
        Services.AddMudServices();
        Services.AddMudPopoverService();
        Services.AddScoped<HttpClient>(_ => new HttpClient { BaseAddress = new Uri("http://localhost") });
        
        // Setup MudBlazor JS interop mocks
        JSInterop.Mode = JSRuntimeMode.Loose;
        
        // Add MudPopoverProvider to the render tree
        RenderTree.Add<MudPopoverProvider>();
    }

    [Fact]
    public void MemoryBrowser_Renders()
    {
        // Arrange & Act
        var cut = RenderComponent<Memory>();

        // Assert
        cut.Markup.Should().Contain("Memory");
    }

    [Fact]
    public void MemoryBrowser_HasSearchInput()
    {
        // Arrange & Act
        var cut = RenderComponent<Memory>();

        // Assert - check the markup contains search-related text
        cut.Markup.Should().Contain("Search memory");
    }

    [Fact]
    public void MemoryBrowser_HasAddMemorySection()
    {
        // Arrange & Act
        var cut = RenderComponent<Memory>();

        // Assert
        cut.Markup.Should().Contain("Add New Memory");
    }

    [Fact]
    public void MemoryBrowser_HasKeyField()
    {
        // Arrange & Act
        var cut = RenderComponent<Memory>();

        // Assert
        cut.Markup.Should().Contain("Key");
    }

    [Fact]
    public void MemoryBrowser_HasContentField()
    {
        // Arrange & Act
        var cut = RenderComponent<Memory>();

        // Assert
        cut.Markup.Should().Contain("Content");
    }
}
