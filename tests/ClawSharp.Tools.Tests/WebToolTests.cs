using System.Text.Json;
using ClawSharp.Core.Tools;
using ClawSharp.TestHelpers;
using FluentAssertions;

namespace ClawSharp.Tools.Tests;

public class WebSearchToolTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsFormattedResults()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        var json = @"{ ""web"": { ""results"": [
            {""title"": ""Test Result"", ""url"": ""https://test.com"", ""description"": ""A test result""}
        ] } }";
        handler.EnqueueJson(json);
        var httpClient = handler.CreateClient("https://api.brave.com/");
        var tool = new WebSearchTool(httpClient, "test-api-key");
        var args = JsonSerializer.Deserialize<JsonElement>(@"{""query"": ""test""}");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Test Result");
        result.Output.Should().Contain("https://test.com");
    }

    [Fact]
    public async Task ExecuteAsync_RespectsCountParameter()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(@"{ ""web"": { ""results"": [] } }");
        var httpClient = handler.CreateClient("https://api.brave.com/");
        var tool = new WebSearchTool(httpClient, "test-api-key");
        var args = JsonSerializer.Deserialize<JsonElement>(@"{""query"": ""test"", ""count"": 3}");

        // Act
        await tool.ExecuteAsync(args);

        // Assert
        var lastRequest = handler.SentRequests.Last();
        lastRequest.RequestUri!.ToString().Should().Contain("count=3");
    }

    [Fact]
    public async Task ExecuteAsync_MissingQuery_ReturnsError()
    {
        // Arrange
        var tool = new WebSearchTool(new HttpClient(), "test-api-key");
        var args = JsonSerializer.Deserialize<JsonElement>("{}");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("query");
    }

    [Fact]
    public async Task ExecuteAsync_ApiError_ReturnsError()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(System.Net.HttpStatusCode.TooManyRequests, @"{""error"": ""Rate limit exceeded""}");
        var httpClient = handler.CreateClient("https://api.brave.com/");
        var tool = new WebSearchTool(httpClient, "test-api-key");
        var args = JsonSerializer.Deserialize<JsonElement>(@"{""query"": ""test""}");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("429");
    }

    [Fact]
    public void Specification_HasCorrectSchema()
    {
        // Arrange
        var tool = new WebSearchTool(new HttpClient(), "test-api-key");

        // Act & Assert
        tool.Name.Should().Be("web_search");
        tool.Specification.Name.Should().Be("web_search");
    }
}

public class WebFetchToolTests
{
    [Fact]
    public async Task ExecuteAsync_ExtractsReadableText()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(System.Net.HttpStatusCode.OK, "<html><body><h1>Title</h1><p>Content here</p></body></html>", "text/html");
        var httpClient = handler.CreateClient("https://example.com/");
        var tool = new WebFetchTool(httpClient);
        var args = JsonSerializer.Deserialize<JsonElement>(@"{""url"": ""https://example.com/page""}");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Title");
        result.Output.Should().Contain("Content here");
    }

    [Fact]
    public async Task ExecuteAsync_StripsScriptTags()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(System.Net.HttpStatusCode.OK, "<html><script>alert('xss')</script><body>Safe content</body></html>", "text/html");
        var httpClient = handler.CreateClient("https://example.com/");
        var tool = new WebFetchTool(httpClient);
        var args = JsonSerializer.Deserialize<JsonElement>(@"{""url"": ""https://example.com/page""}");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Output.Should().NotContain("alert");
        result.Output.Should().Contain("Safe content");
    }

    [Fact]
    public async Task ExecuteAsync_TruncatesLargeContent()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        var largeContent = "<html><body>" + new string('x', 50000) + "</body></html>";
        handler.EnqueueResponse(System.Net.HttpStatusCode.OK, largeContent, "text/html");
        var httpClient = handler.CreateClient("https://example.com/");
        var tool = new WebFetchTool(httpClient);
        var args = JsonSerializer.Deserialize<JsonElement>(@"{""url"": ""https://example.com/page"", ""max_chars"": 1000}");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Output.Length.Should().BeLessThanOrEqualTo(1050); // 1000 + truncation msg
    }

    [Fact]
    public async Task ExecuteAsync_MissingUrl_ReturnsError()
    {
        // Arrange
        var tool = new WebFetchTool(new HttpClient());
        var args = JsonSerializer.Deserialize<JsonElement>("{}");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("url");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidUrl_ReturnsError()
    {
        // Arrange
        var tool = new WebFetchTool(new HttpClient());
        var args = JsonSerializer.Deserialize<JsonElement>(@"{""url"": ""not-a-valid-url""}");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Invalid URL");
    }

    [Fact]
    public async Task ExecuteAsync_NonHtmlContent_ExtractsText()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(System.Net.HttpStatusCode.OK, "Plain text content", "text/plain");
        var httpClient = handler.CreateClient("https://example.com/");
        var tool = new WebFetchTool(httpClient);
        var args = JsonSerializer.Deserialize<JsonElement>(@"{""url"": ""https://example.com/file.txt""}");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Plain text content");
    }

    [Fact]
    public async Task ExecuteAsync_StripsStyleTags()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(System.Net.HttpStatusCode.OK, "<html><style>.foo { color: red; }</style><body>Content</body></html>", "text/html");
        var httpClient = handler.CreateClient("https://example.com/");
        var tool = new WebFetchTool(httpClient);
        var args = JsonSerializer.Deserialize<JsonElement>(@"{""url"": ""https://example.com/page""}");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Output.Should().NotContain("color: red");
        result.Output.Should().Contain("Content");
    }

    [Fact]
    public void Specification_HasCorrectSchema()
    {
        // Arrange
        var tool = new WebFetchTool(new HttpClient());

        // Act & Assert
        tool.Name.Should().Be("web_fetch");
        tool.Specification.Name.Should().Be("web_fetch");
    }
}
