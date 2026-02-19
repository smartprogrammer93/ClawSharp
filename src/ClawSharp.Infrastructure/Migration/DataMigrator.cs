namespace ClawSharp.Infrastructure.Migration;

/// <summary>
/// Handles data migration from OpenClaw, ZeroClaw, and PicoClaw installations.
/// </summary>
public class DataMigrator
{
    /// <summary>
    /// Detects the source format based on directory structure.
    /// </summary>
    public static MigrationSourceFormat DetectSourceFormat(string sourcePath)
    {
        if (!Directory.Exists(sourcePath))
        {
            return MigrationSourceFormat.Unknown;
        }

        // Check for OpenClaw: has agents/main directory structure
        if (Directory.Exists(Path.Combine(sourcePath, "agents", "main")))
        {
            return MigrationSourceFormat.OpenClaw;
        }

        // Check for ZeroClaw: has .zeroclaw marker file
        if (File.Exists(Path.Combine(sourcePath, ".zeroclaw")))
        {
            return MigrationSourceFormat.ZeroClaw;
        }

        // Check for PicoClaw: has picoclaw.json config
        if (File.Exists(Path.Combine(sourcePath, "picoclaw.json")))
        {
            return MigrationSourceFormat.PicoClaw;
        }

        return MigrationSourceFormat.Unknown;
    }

    /// <summary>
    /// Migrates data from source to target directory.
    /// </summary>
    public async Task<MigrationResult> MigrateAsync(
        string sourcePath,
        string targetPath,
        MigrationOptions options)
    {
        var result = new MigrationResult { Success = true };

        var format = DetectSourceFormat(sourcePath);

        try
        {
            // Migrate memories
            if (options.MigrateMemories)
            {
                await MigrateMemoriesAsync(sourcePath, targetPath, options, result, format);
            }

            // Migrate sessions
            if (options.MigrateSessions)
            {
                await MigrateSessionsAsync(sourcePath, targetPath, options, result, format);
            }

            // Migrate config
            if (options.MigrateConfig)
            {
                await MigrateConfigAsync(sourcePath, targetPath, options, result, format);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    private async Task MigrateMemoriesAsync(
        string sourcePath,
        string targetPath,
        MigrationOptions options,
        MigrationResult result,
        MigrationSourceFormat format)
    {
        var sourceMemoryDir = GetMemoryDirectory(sourcePath, format);
        if (!Directory.Exists(sourceMemoryDir))
        {
            return;
        }

        var targetMemoryDir = Path.Combine(targetPath, "workspace", "memory");

        var memoryFiles = Directory.GetFiles(sourceMemoryDir, "*.md", SearchOption.AllDirectories);

        foreach (var sourceFile in memoryFiles)
        {
            var relativePath = Path.GetRelativePath(sourceMemoryDir, sourceFile);
            var targetFile = Path.Combine(targetMemoryDir, relativePath);

            var copied = await CopyFileWithOptionsAsync(sourceFile, targetFile, options, result);
            if (copied)
            {
                result.MemoriesImported++;
            }
        }
    }

    private async Task MigrateSessionsAsync(
        string sourcePath,
        string targetPath,
        MigrationOptions options,
        MigrationResult result,
        MigrationSourceFormat format)
    {
        var sourceSessionsDir = GetSessionsDirectory(sourcePath, format);
        if (!Directory.Exists(sourceSessionsDir))
        {
            return;
        }

        var targetSessionsDir = Path.Combine(targetPath, "agents", "main", "sessions");

        var sessionFiles = Directory.GetFiles(sourceSessionsDir, "*.jsonl", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(sourceSessionsDir, "*.json", SearchOption.AllDirectories));

        foreach (var sourceFile in sessionFiles)
        {
            var relativePath = Path.GetRelativePath(sourceSessionsDir, sourceFile);
            var targetFile = Path.Combine(targetSessionsDir, relativePath);

            var copied = await CopyFileWithOptionsAsync(sourceFile, targetFile, options, result);
            if (copied)
            {
                result.SessionsImported++;
            }
        }
    }

    private async Task MigrateConfigAsync(
        string sourcePath,
        string targetPath,
        MigrationOptions options,
        MigrationResult result,
        MigrationSourceFormat format)
    {
        var sourceConfig = GetConfigPath(sourcePath, format);
        if (!File.Exists(sourceConfig))
        {
            return;
        }

        var targetConfig = Path.Combine(targetPath, "config.toml");
        await CopyFileWithOptionsAsync(sourceConfig, targetConfig, options, result);
    }

    private async Task<bool> CopyFileWithOptionsAsync(
        string sourceFile,
        string targetFile,
        MigrationOptions options,
        MigrationResult result)
    {
        var targetExists = File.Exists(targetFile);

        if (targetExists)
        {
            switch (options.ConflictResolution)
            {
                case ConflictResolution.Skip:
                    result.FilesSkipped++;
                    return false;

                case ConflictResolution.Overwrite:
                    result.FilesOverwritten++;
                    break;

                case ConflictResolution.Rename:
                    targetFile = GetUniqueFileName(targetFile);
                    break;
            }
        }

        if (options.DryRun)
        {
            return true; // Report as would-be-copied
        }

        // Ensure target directory exists
        var targetDir = Path.GetDirectoryName(targetFile);
        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // Copy file
        await Task.Run(() => File.Copy(sourceFile, targetFile, overwrite: true));

        // Preserve timestamps if requested
        if (options.PreserveTimestamps)
        {
            var sourceInfo = new FileInfo(sourceFile);
            File.SetLastWriteTimeUtc(targetFile, sourceInfo.LastWriteTimeUtc);
            File.SetCreationTimeUtc(targetFile, sourceInfo.CreationTimeUtc);
        }

        return true;
    }

    private static string GetMemoryDirectory(string basePath, MigrationSourceFormat format)
    {
        return format switch
        {
            MigrationSourceFormat.OpenClaw => Path.Combine(basePath, "workspace", "memory"),
            MigrationSourceFormat.ZeroClaw => Path.Combine(basePath, "data", "memory"),
            MigrationSourceFormat.PicoClaw => Path.Combine(basePath, "memory"),
            _ => Path.Combine(basePath, "workspace", "memory")
        };
    }

    private static string GetSessionsDirectory(string basePath, MigrationSourceFormat format)
    {
        return format switch
        {
            MigrationSourceFormat.OpenClaw => Path.Combine(basePath, "agents", "main", "sessions"),
            MigrationSourceFormat.ZeroClaw => Path.Combine(basePath, "data", "sessions"),
            MigrationSourceFormat.PicoClaw => Path.Combine(basePath, "sessions"),
            _ => Path.Combine(basePath, "agents", "main", "sessions")
        };
    }

    private static string GetConfigPath(string basePath, MigrationSourceFormat format)
    {
        return format switch
        {
            MigrationSourceFormat.OpenClaw => Path.Combine(basePath, "config.toml"),
            MigrationSourceFormat.ZeroClaw => Path.Combine(basePath, "zeroclaw.toml"),
            MigrationSourceFormat.PicoClaw => Path.Combine(basePath, "picoclaw.json"),
            _ => Path.Combine(basePath, "config.toml")
        };
    }

    private static string GetUniqueFileName(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath) ?? "";
        var name = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath);
        var counter = 1;

        string newPath;
        do
        {
            newPath = Path.Combine(dir, $"{name}_{counter}{ext}");
            counter++;
        } while (File.Exists(newPath));

        return newPath;
    }
}
