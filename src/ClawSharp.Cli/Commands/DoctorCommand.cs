using System.CommandLine;

namespace ClawSharp.Cli.Commands;

public class DoctorCommand : Command
{
    public DoctorCommand() : base("doctor", "Run diagnostics")
    {
        SetAction(_ => Execute());
    }

    private static void Execute()
    {
        Console.WriteLine("  ClawSharp Doctor");
        Console.WriteLine("  ================");

        var checks = new List<(string Name, bool Pass, string Detail)>();

        var homePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clawsharp");

        var configPath = Environment.GetEnvironmentVariable("CLAWSHARP_CONFIG_PATH")
            ?? Path.Combine(homePath, "config.toml");
        checks.Add(("Config file", File.Exists(configPath), configPath));

        checks.Add(("Data directory", Directory.Exists(homePath), homePath));

        var workspacePath = Path.Combine(homePath, "workspace");
        checks.Add(("Workspace directory", Directory.Exists(workspacePath), workspacePath));

        // SQLite check
        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            conn.Open();
            checks.Add(("SQLite", true, "Available"));
        }
        catch (Exception ex)
        {
            checks.Add(("SQLite", false, ex.Message));
        }

        foreach (var (name, pass, detail) in checks)
        {
            var icon = pass ? "✅" : "❌";
            Console.WriteLine($"  {icon} {name}: {detail}");
        }
    }
}
