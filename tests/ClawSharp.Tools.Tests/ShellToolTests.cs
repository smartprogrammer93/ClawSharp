using System.Text.Json;
using ClawSharp.Core.Security;
using ClawSharp.Core.Tools;
using FluentAssertions;
using NSubstitute;

namespace ClawSharp.Tools.Tests;

public class ShellToolTests
{
    [Fact]
    public async Task ExecuteAsync_AllowedCommand_ReturnsOutput()
    {
        // Arrange
        var security = Substitute.For<ISecurityPolicy>();
        security.IsCommandAllowed(Arg.Any<string>()).Returns(true);
        var tool = new ShellTool(security);
        var args = JsonSerializer.Deserialize<JsonElement>("""{"command": "echo hello"}""");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("hello");
    }

    [Fact]
    public async Task ExecuteAsync_DisallowedCommand_ReturnsError()
    {
        // Arrange
        var security = Substitute.For<ISecurityPolicy>();
        security.IsCommandAllowed(Arg.Any<string>()).Returns(false);
        var tool = new ShellTool(security);
        var args = JsonSerializer.Deserialize<JsonElement>("""{"command": "rm -rf /"}""");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not allowed");
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_KillsProcess()
    {
        // Arrange
        var security = Substitute.For<ISecurityPolicy>();
        security.IsCommandAllowed(Arg.Any<string>()).Returns(true);
        var tool = new ShellTool(security, TimeSpan.FromMilliseconds(100));
        var args = JsonSerializer.Deserialize<JsonElement>("""{"command": "sleep 10"}""");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("timed out");
    }

    [Fact]
    public async Task ExecuteAsync_LargeOutput_Truncates()
    {
        // Arrange
        var security = Substitute.For<ISecurityPolicy>();
        security.IsCommandAllowed(Arg.Any<string>()).Returns(true);
        var tool = new ShellTool(security);
        var args = JsonSerializer.Deserialize<JsonElement>("""{"command": "seq 1 100000"}""");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Output.Length.Should().BeLessThanOrEqualTo(11_000); // 10KB + truncation message
    }

    [Fact]
    public void Specification_HasCorrectSchema()
    {
        // Arrange
        var security = Substitute.For<ISecurityPolicy>();
        var tool = new ShellTool(security);

        // Act & Assert
        tool.Name.Should().Be("shell");
        tool.Specification.Name.Should().Be("shell");
        tool.Description.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_MissingCommandParameter_ReturnsError()
    {
        // Arrange
        var security = Substitute.For<ISecurityPolicy>();
        var tool = new ShellTool(security);
        var args = JsonSerializer.Deserialize<JsonElement>("""{}""");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("command");
    }

    [Fact]
    public async Task ExecuteAsync_NonZeroExitCode_IncludesExitCodeInOutput()
    {
        // Arrange
        var security = Substitute.For<ISecurityPolicy>();
        security.IsCommandAllowed(Arg.Any<string>()).Returns(true);
        var tool = new ShellTool(security);
        var args = JsonSerializer.Deserialize<JsonElement>("""{"command": "exit 42"}""");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("exit code");
        result.Error.Should().Contain("42");
    }

    [Fact]
    public async Task ExecuteAsync_CapturesStderr()
    {
        // Arrange
        var security = Substitute.For<ISecurityPolicy>();
        security.IsCommandAllowed(Arg.Any<string>()).Returns(true);
        var tool = new ShellTool(security);
        var args = JsonSerializer.Deserialize<JsonElement>("""{"command": "echo error >&2"}""");

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("error");
    }
}
