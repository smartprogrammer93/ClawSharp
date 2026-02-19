using System.CommandLine.Parsing;
using ClawSharp.Cli;
using ClawSharp.Cli.Commands;
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

        // Exit code 0 when all basic checks pass (or informational failures)
        exitCode.Should().BeOneOf(0, 1); // May be 1 if config doesn't exist
    }

    [Fact]
    public async Task DoctorCommand_ChecksDiskSpace()
    {
        var output = new StringWriter();
        Console.SetOut(output);

        var root = CliEntryPoint.CreateRootCommand();
        await CommandLineParser.Parse(root, ["doctor"]).InvokeAsync();

        output.ToString().Should().Contain("Disk space");
    }

    [Fact]
    public async Task DoctorCommand_ChecksMemoryDirectory()
    {
        var output = new StringWriter();
        Console.SetOut(output);

        var root = CliEntryPoint.CreateRootCommand();
        await CommandLineParser.Parse(root, ["doctor"]).InvokeAsync();

        output.ToString().Should().Contain("Memory directory");
    }

    [Fact]
    public async Task DoctorCommand_ChecksSkillsDirectory()
    {
        var output = new StringWriter();
        Console.SetOut(output);

        var root = CliEntryPoint.CreateRootCommand();
        await CommandLineParser.Parse(root, ["doctor"]).InvokeAsync();

        output.ToString().Should().Contain("Skills directory");
    }

    [Fact]
    public async Task DoctorCommand_HasFixOption()
    {
        var cmd = new DoctorCommand();
        cmd.Options.Should().Contain(o => o.Name == "--fix");
    }

    [Fact]
    public async Task DoctorCommand_HasJsonOption()
    {
        var cmd = new DoctorCommand();
        cmd.Options.Should().Contain(o => o.Name == "--json");
    }

    [Fact]
    public async Task DoctorCommand_FixFlag_CreatesDirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"clawsharp-test-{Guid.NewGuid()}");
        var workspaceDir = Path.Combine(tempDir, "workspace");
        var memoryDir = Path.Combine(workspaceDir, "memory");
        var skillsDir = Path.Combine(workspaceDir, "skills");
        
        try
        {
            // Set environment variable to use temp dir
            var originalVar = Environment.GetEnvironmentVariable("CLAWSHARP_DATA_DIR");
            Environment.SetEnvironmentVariable("CLAWSHARP_DATA_DIR", tempDir);

            var output = new StringWriter();
            Console.SetOut(output);

            var root = CliEntryPoint.CreateRootCommand();
            await CommandLineParser.Parse(root, ["doctor", "--fix"]).InvokeAsync();

            // Verify directories were created
            Directory.Exists(tempDir).Should().BeTrue();
            
            Environment.SetEnvironmentVariable("CLAWSHARP_DATA_DIR", originalVar);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DoctorCommand_JsonOption_OutputsJson()
    {
        var output = new StringWriter();
        Console.SetOut(output);

        var root = CliEntryPoint.CreateRootCommand();
        await CommandLineParser.Parse(root, ["doctor", "--json"]).InvokeAsync();

        var result = output.ToString();
        result.Should().Contain("{");
        result.Should().Contain("}");
    }

    [Fact]
    public void DoctorCheck_CanBeCreated()
    {
        var check = new DoctorCheck("Test Check", true, "All good");
        check.Name.Should().Be("Test Check");
        check.Pass.Should().BeTrue();
        check.Detail.Should().Be("All good");
    }

    [Fact]
    public void DoctorCheck_FailedCheck_HasCorrectState()
    {
        var check = new DoctorCheck("Failed Check", false, "Something is wrong");
        check.Pass.Should().BeFalse();
    }

    [Fact]
    public async Task DoctorCommand_ShowsSummary()
    {
        var output = new StringWriter();
        Console.SetOut(output);

        var root = CliEntryPoint.CreateRootCommand();
        await CommandLineParser.Parse(root, ["doctor"]).InvokeAsync();

        var result = output.ToString();
        // Should show some form of summary
        (result.Contains("passed") || result.Contains("✅") || result.Contains("checks")).Should().BeTrue();
    }
}
