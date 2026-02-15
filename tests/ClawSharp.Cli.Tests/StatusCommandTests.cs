using System.CommandLine.Parsing;
using ClawSharp.Cli;
using FluentAssertions;

namespace ClawSharp.Cli.Tests;

public class StatusCommandTests
{
    [Fact]
    public async Task StatusCommand_OutputsVersionInfo()
    {
        var output = new StringWriter();
        Console.SetOut(output);

        var root = CliEntryPoint.CreateRootCommand();
        await CommandLineParser.Parse(root, ["status"]).InvokeAsync();

        var result = output.ToString();
        result.Should().Contain("ClawSharp");
        result.Should().Contain("Runtime:");
        result.Should().Contain("OS:");
    }

    [Fact]
    public async Task StatusCommand_OutputsConfigPath()
    {
        var output = new StringWriter();
        Console.SetOut(output);

        var root = CliEntryPoint.CreateRootCommand();
        await CommandLineParser.Parse(root, ["status"]).InvokeAsync();

        var result = output.ToString();
        result.Should().Contain("Config:");
        result.Should().Contain("Workspace:");
    }
}
