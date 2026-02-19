using System.CommandLine;
using System.Text.Json;

namespace ClawSharp.Cli.Commands;

/// <summary>
/// Represents a single diagnostic check result.
/// </summary>
public record DoctorCheck(string Name, bool Pass, string Detail);

/// <summary>
/// Comprehensive system diagnostics command.
/// </summary>
public class DoctorCommand : Command
{
    public DoctorCommand() : base("doctor", "Run comprehensive system diagnostics")
    {
        var fixOption = new Option<bool>("--fix");
        fixOption.Description = "Attempt to auto-fix issues (create missing directories, etc.)";

        var jsonOption = new Option<bool>("--json");
        jsonOption.Description = "Output results as JSON";

        Options.Add(fixOption);
        Options.Add(jsonOption);

        SetAction(ctx =>
        {
            var fix = ctx.GetValue(fixOption);
            var json = ctx.GetValue(jsonOption);
            return Execute(fix, json);
        });
    }

    private static int Execute(bool fix, bool json)
    {
        var checks = new List<DoctorCheck>();

        // Determine base paths
        var dataDir = Environment.GetEnvironmentVariable("CLAWSHARP_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clawsharp");

        var configPath = Environment.GetEnvironmentVariable("CLAWSHARP_CONFIG_PATH")
            ?? Path.Combine(dataDir, "config.toml");

        var workspacePath = Path.Combine(dataDir, "workspace");
        var memoryPath = Path.Combine(workspacePath, "memory");
        var skillsPath = Path.Combine(workspacePath, "skills");

        // Check: Data directory
        var dataExists = Directory.Exists(dataDir);
        if (!dataExists && fix)
        {
            Directory.CreateDirectory(dataDir);
            dataExists = true;
        }
        checks.Add(new DoctorCheck("Data directory", dataExists, dataDir));

        // Check: Config file
        var configExists = File.Exists(configPath);
        checks.Add(new DoctorCheck("Config file", configExists, configPath));

        // Check: Workspace directory
        var workspaceExists = Directory.Exists(workspacePath);
        if (!workspaceExists && fix)
        {
            Directory.CreateDirectory(workspacePath);
            workspaceExists = true;
        }
        checks.Add(new DoctorCheck("Workspace directory", workspaceExists, workspacePath));

        // Check: Memory directory
        var memoryExists = Directory.Exists(memoryPath);
        if (!memoryExists && fix)
        {
            Directory.CreateDirectory(memoryPath);
            memoryExists = true;
        }
        checks.Add(new DoctorCheck("Memory directory", memoryExists, memoryPath));

        // Check: Skills directory
        var skillsExists = Directory.Exists(skillsPath);
        if (!skillsExists && fix)
        {
            Directory.CreateDirectory(skillsPath);
            skillsExists = true;
        }
        checks.Add(new DoctorCheck("Skills directory", skillsExists, skillsPath));

        // Check: SQLite availability
        bool sqliteOk;
        string sqliteDetail;
        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            conn.Open();
            sqliteOk = true;
            sqliteDetail = "Available";
        }
        catch (Exception ex)
        {
            sqliteOk = false;
            sqliteDetail = ex.Message;
        }
        checks.Add(new DoctorCheck("SQLite", sqliteOk, sqliteDetail));

        // Check: Disk space
        bool diskOk;
        string diskDetail;
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(dataDir) ?? "/");
            var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            diskOk = freeGb > 1.0; // At least 1 GB free
            diskDetail = $"{freeGb:F2} GB free";
        }
        catch (Exception ex)
        {
            diskOk = true; // Can't check, assume OK
            diskDetail = $"Unable to check: {ex.Message}";
        }
        checks.Add(new DoctorCheck("Disk space", diskOk, diskDetail));

        // Output results
        if (json)
        {
            OutputJson(checks);
        }
        else
        {
            OutputText(checks);
        }

        // Return exit code based on critical failures
        var criticalFailures = checks.Count(c => !c.Pass && IsCritical(c.Name));
        return criticalFailures > 0 ? 1 : 0;
    }

    private static bool IsCritical(string checkName)
    {
        // Data directory and SQLite are critical
        return checkName is "Data directory" or "SQLite";
    }

    private static void OutputJson(List<DoctorCheck> checks)
    {
        var result = new
        {
            checks = checks.Select(c => new
            {
                name = c.Name,
                pass = c.Pass,
                detail = c.Detail
            }).ToArray(),
            summary = new
            {
                total = checks.Count,
                passed = checks.Count(c => c.Pass),
                failed = checks.Count(c => !c.Pass)
            }
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        Console.WriteLine(JsonSerializer.Serialize(result, options));
    }

    private static void OutputText(List<DoctorCheck> checks)
    {
        Console.WriteLine("  ClawSharp Doctor");
        Console.WriteLine("  ================");
        Console.WriteLine();

        foreach (var check in checks)
        {
            var icon = check.Pass ? "✅" : "❌";
            Console.WriteLine($"  {icon} {check.Name}: {check.Detail}");
        }

        Console.WriteLine();
        var passed = checks.Count(c => c.Pass);
        var total = checks.Count;
        Console.WriteLine($"  Summary: {passed}/{total} checks passed");
    }
}
