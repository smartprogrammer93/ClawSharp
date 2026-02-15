using System.CommandLine.Parsing;
using ClawSharp.Cli;
using FluentAssertions;

namespace ClawSharp.Cli.Tests;

public class RootCommandTests
{
    [Fact]
    public async Task Help_ShowsAllCommands()
    {
        var console = new StringWriter();
        Console.SetOut(console);

        var root = CliEntryPoint.CreateRootCommand();
        var result = CommandLineParser.Parse(root, ["--help"]);
        await result.InvokeAsync();

        var output = console.ToString();
        output.Should().Contain("status");
        output.Should().Contain("doctor");
        output.Should().Contain("agent");
        output.Should().Contain("gateway");
        output.Should().Contain("onboard");
    }

    [Fact]
    public async Task Version_PrintsVersion()
    {
        var console = new StringWriter();
        Console.SetOut(console);

        var root = CliEntryPoint.CreateRootCommand();
        var result = CommandLineParser.Parse(root, ["--version"]);
        await result.InvokeAsync();

        var output = console.ToString();
        output.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GlobalOption_Config_IsAccepted()
    {
        var console = new StringWriter();
        Console.SetOut(console);

        var root = CliEntryPoint.CreateRootCommand();
        var result = CommandLineParser.Parse(root, ["--config", "/tmp/test.toml", "status"]);
        var exitCode = await result.InvokeAsync();
        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task GlobalOption_Verbose_IsAccepted()
    {
        var console = new StringWriter();
        Console.SetOut(console);

        var root = CliEntryPoint.CreateRootCommand();
        var result = CommandLineParser.Parse(root, ["--verbose", "status"]);
        var exitCode = await result.InvokeAsync();
        exitCode.Should().Be(0);
    }
}
