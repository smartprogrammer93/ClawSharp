using System.CommandLine;

namespace ClawSharp.Cli.Commands;

public class GatewayCommand : Command
{
    public GatewayCommand() : base("gateway", "Manage the gateway service")
    {
        SetAction(_ => Console.WriteLine("  Gateway command not yet implemented."));
    }
}
