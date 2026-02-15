using System.CommandLine;
using System.Runtime.InteropServices;

namespace ClawSharp.Cli.Commands;

public class StatusCommand : Command
{
    public StatusCommand() : base("status", "Show system status")
    {
        SetAction(_ => Execute());
    }

    private static void Execute()
    {
        var version = typeof(StatusCommand).Assembly.GetName().Version;
        Console.WriteLine($"  ClawSharp v{version}");
        Console.WriteLine($"  Runtime:   .NET {Environment.Version}");
        Console.WriteLine($"  OS:        {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        Console.WriteLine($"  Config:    {GetConfigPath()}");
        Console.WriteLine($"  Workspace: {GetWorkspacePath()}");
    }

    private static string GetConfigPath() =>
        Environment.GetEnvironmentVariable("CLAWSHARP_CONFIG_PATH")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clawsharp", "config.toml");

    private static string GetWorkspacePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clawsharp", "workspace");
}
