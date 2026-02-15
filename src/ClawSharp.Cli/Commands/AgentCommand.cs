using System.CommandLine;

namespace ClawSharp.Cli.Commands;

public class AgentCommand : Command
{
    public AgentCommand() : base("agent", "Manage the AI agent")
    {
        SetAction(_ => Console.WriteLine("  Agent command not yet implemented."));
    }
}
