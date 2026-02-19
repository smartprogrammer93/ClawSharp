using System.CommandLine;
using ClawSharp.Infrastructure.Migration;

namespace ClawSharp.Cli.Commands;

/// <summary>
/// Command for migrating data from OpenClaw, ZeroClaw, or PicoClaw installations.
/// </summary>
public class MigrateCommand : Command
{
    public MigrateCommand() : base("migrate", "Migrate data from another ClawSharp/OpenClaw installation")
    {
        var fromOption = new Option<string>("--from");
        fromOption.Description = "Source directory path (required)";

        var toOption = new Option<string>("--to");
        toOption.Description = "Target directory path (defaults to current ClawSharp data dir)";

        var dryRunOption = new Option<bool>("--dry-run");
        dryRunOption.Description = "Show what would be migrated without making changes";

        var overwriteOption = new Option<bool>("--overwrite");
        overwriteOption.Description = "Overwrite existing files instead of skipping";

        var configOption = new Option<bool>("--include-config");
        configOption.Description = "Also migrate configuration files";

        Options.Add(fromOption);
        Options.Add(toOption);
        Options.Add(dryRunOption);
        Options.Add(overwriteOption);
        Options.Add(configOption);

        SetAction(async ctx =>
        {
            var from = ctx.GetValue(fromOption)!;
            var to = ctx.GetValue(toOption);
            var dryRun = ctx.GetValue(dryRunOption);
            var overwrite = ctx.GetValue(overwriteOption);
            var includeConfig = ctx.GetValue(configOption);

            return await ExecuteAsync(from, to, dryRun, overwrite, includeConfig);
        });
    }

    private static async Task<int> ExecuteAsync(
        string from,
        string? to,
        bool dryRun,
        bool overwrite,
        bool includeConfig)
    {
        Console.WriteLine("  ClawSharp Data Migration");
        Console.WriteLine("  ========================");
        Console.WriteLine();

        // Validate source
        if (!Directory.Exists(from))
        {
            Console.WriteLine($"  ❌ Source directory not found: {from}");
            return 1;
        }

        // Detect format
        var format = DataMigrator.DetectSourceFormat(from);
        Console.WriteLine($"  Detected format: {format}");

        if (format == MigrationSourceFormat.Unknown)
        {
            Console.WriteLine("  ❌ Could not detect source format. Is this a valid ClawSharp/OpenClaw installation?");
            return 1;
        }

        // Determine target
        to ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".clawsharp");

        Console.WriteLine($"  Source: {from}");
        Console.WriteLine($"  Target: {to}");
        Console.WriteLine();

        if (dryRun)
        {
            Console.WriteLine("  🔍 Dry run mode - no changes will be made");
            Console.WriteLine();
        }

        var options = new MigrationOptions
        {
            DryRun = dryRun,
            ConflictResolution = overwrite ? ConflictResolution.Overwrite : ConflictResolution.Skip,
            MigrateConfig = includeConfig,
            PreserveTimestamps = true
        };

        var migrator = new DataMigrator();
        var result = await migrator.MigrateAsync(from, to, options);

        // Display results
        Console.WriteLine("  Results:");
        Console.WriteLine($"    Memories imported: {result.MemoriesImported}");
        Console.WriteLine($"    Sessions imported: {result.SessionsImported}");
        Console.WriteLine($"    Files skipped: {result.FilesSkipped}");
        Console.WriteLine($"    Files overwritten: {result.FilesOverwritten}");
        Console.WriteLine();

        if (result.Errors.Count > 0)
        {
            Console.WriteLine("  Errors:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"    ❌ {error}");
            }
            Console.WriteLine();
        }

        if (result.Success)
        {
            Console.WriteLine($"  ✅ Migration completed successfully! ({result.TotalImported} files)");
        }
        else
        {
            Console.WriteLine("  ❌ Migration completed with errors.");
        }

        return result.Success ? 0 : 1;
    }
}
