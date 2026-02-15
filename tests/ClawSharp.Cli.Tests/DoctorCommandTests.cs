using System.CommandLine.Parsing;
using ClawSharp.Cli;
using FluentAssertions;

namespace ClawSharp.Cli.Tests;

public class DoctorCommandTests
{
    [Fact]
    public async Task DoctorCommand_ChecksSqlite()
    {
        var output = new StringWriter();
        Console.SetOut(output);

        var root = CliEntryPoint.CreateRootCommand();
        await CommandLineParser.Parse(root, ["doctor"]).InvokeAsync();

        output.ToString().Should().Contain("SQLite");
    }

    [Fact]
    public async Task DoctorCommand_ChecksConfigFile()
    {
        var output = new StringWriter();
        Console.SetOut(output);

        var root = CliEntryPoint.CreateRootCommand();
        await CommandLineParser.Parse(root, ["doctor"]).InvokeAsync();

        output.ToString().Should().Contain("Config file");
    }

    [Fact]
    public async Task DoctorCommand_ChecksDirectories()
    {
        var output = new StringWriter();
        Console.SetOut(output);

        var root = CliEntryPoint.CreateRootCommand();
        await CommandLineParser.Parse(root, ["doctor"]).InvokeAsync();

        output.ToString().Should().Contain("Data directory");
    }

    [Fact]
    public async Task DoctorCommand_ReturnsZeroExitCode()
    {
        var output = new StringWriter();
        Console.SetOut(output);

        var root = CliEntryPoint.CreateRootCommand();
        var exitCode = await CommandLineParser.Parse(root, ["doctor"]).InvokeAsync();

        exitCode.Should().Be(0);
    }
}
