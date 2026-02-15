using System.CommandLine;

namespace ClawSharp.Cli.Commands;

public class OnboardCommand : Command
{
    public OnboardCommand() : base("onboard", "Run the onboarding wizard")
    {
        SetAction(_ => Console.WriteLine("  Onboarding wizard not yet implemented."));
    }
}
