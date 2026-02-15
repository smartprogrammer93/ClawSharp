using Bunit;
using ClawSharp.UI.Pages;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace ClawSharp.UI.Tests;

public class ChatTests : TestContext
{
    public ChatTests()
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
    public void Chat_RendersInputAndButton()
    {
        // Arrange & Act
        var cut = RenderComponent<Chat>();

        // Assert
        var textField = cut.FindComponents<MudTextField<string>>()
            .FirstOrDefault(c => c.Instance.Label == "Type your message...");
        textField.Should().NotBeNull();
        
        var sendButton = cut.FindComponents<MudButton>()
            .FirstOrDefault(c => c.Markup.Contains("Send"));
        sendButton.Should().NotBeNull();
    }

    [Fact]
    public void Chat_SendButton_DisabledWhenEmpty()
    {
        // Arrange & Act
        var cut = RenderComponent<Chat>();

        // Assert
        var sendButton = cut.FindComponents<MudButton>()
            .FirstOrDefault(c => c.Markup.Contains("Send"));
        sendButton.Should().NotBeNull();
        sendButton!.Instance.Disabled.Should().BeTrue();
    }

    [Fact]
    public void Chat_HasSessionKeyInput()
    {
        // Arrange & Act
        var cut = RenderComponent<Chat>();

        // Assert
        var sessionKeyInput = cut.FindComponents<MudTextField<string>>()
            .FirstOrDefault(c => c.Instance.Label == "Session Key");
        sessionKeyInput.Should().NotBeNull();
    }

    [Fact]
    public void Chat_HasOptionsPanel()
    {
        // Arrange & Act
        var cut = RenderComponent<Chat>();

        // Assert
        var optionsHeading = cut.FindAll("h6").FirstOrDefault(e => e.TextContent.Contains("Options"));
        optionsHeading.Should().NotBeNull();
    }
}
