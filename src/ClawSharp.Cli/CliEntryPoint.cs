using System.CommandLine;
using ClawSharp.Cli.Commands;

namespace ClawSharp.Cli;

public static class CliEntryPoint
{
    public static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("ClawSharp — .NET AI Assistant Platform");

        rootCommand.Add(new StatusCommand());
        rootCommand.Add(new DoctorCommand());
        rootCommand.Add(new AgentCommand());
        rootCommand.Add(new GatewayCommand());
        rootCommand.Add(new OnboardCommand());
        rootCommand.Add(new ServiceCommand());

        var configOption = new Option<string>("--config") { Description = "Path to config.toml" };
        var verboseOption = new Option<bool>("--verbose") { Description = "Enable verbose logging" };

        rootCommand.Options.Add(configOption);
        rootCommand.Options.Add(verboseOption);

        return rootCommand;
    }
}
