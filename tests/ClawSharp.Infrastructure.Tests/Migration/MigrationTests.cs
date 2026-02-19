using ClawSharp.Infrastructure.Migration;
using FluentAssertions;

namespace ClawSharp.Infrastructure.Tests.Migration;

public class MigrationTests
{
    [Fact]
    public void DetectSourceFormat_OpenClaw_Detected()
    {
        var tempDir = CreateOpenClawLayout();
        try
        {
            var format = DataMigrator.DetectSourceFormat(tempDir);
            format.Should().Be(MigrationSourceFormat.OpenClaw);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DetectSourceFormat_ZeroClaw_Detected()
    {
        var tempDir = CreateZeroClawLayout();
        try
        {
            var format = DataMigrator.DetectSourceFormat(tempDir);
            format.Should().Be(MigrationSourceFormat.ZeroClaw);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DetectSourceFormat_PicoClaw_Detected()
    {
        var tempDir = CreatePicoClawLayout();
        try
        {
            var format = DataMigrator.DetectSourceFormat(tempDir);
            format.Should().Be(MigrationSourceFormat.PicoClaw);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DetectSourceFormat_Unknown_ReturnsUnknown()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var format = DataMigrator.DetectSourceFormat(tempDir);
            format.Should().Be(MigrationSourceFormat.Unknown);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DetectSourceFormat_NonExistentPath_ReturnsUnknown()
    {
        var format = DataMigrator.DetectSourceFormat("/nonexistent/path");
        format.Should().Be(MigrationSourceFormat.Unknown);
    }

    [Fact]
    public async Task MigrateAsync_ImportsMemories()
    {
        var sourceDir = CreateOpenClawLayout();
        var targetDir = Path.Combine(Path.GetTempPath(), $"target-{Guid.NewGuid()}");
        Directory.CreateDirectory(targetDir);
        
        // Create a memory file in source
        var memoryDir = Path.Combine(sourceDir, "workspace", "memory");
        Directory.CreateDirectory(memoryDir);
        File.WriteAllText(Path.Combine(memoryDir, "2024-01-01.md"), "# Test Memory\n\nSome content.");

        try
        {
            var migrator = new DataMigrator();
            var result = await migrator.MigrateAsync(sourceDir, targetDir, new MigrationOptions());

            result.Success.Should().BeTrue();
            result.MemoriesImported.Should().Be(1);
            
            var targetMemoryFile = Path.Combine(targetDir, "workspace", "memory", "2024-01-01.md");
            File.Exists(targetMemoryFile).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(sourceDir, true);
            Directory.Delete(targetDir, true);
        }
    }

    [Fact]
    public async Task MigrateAsync_PreservesTimestamps()
    {
        var sourceDir = CreateOpenClawLayout();
        var targetDir = Path.Combine(Path.GetTempPath(), $"target-{Guid.NewGuid()}");
        Directory.CreateDirectory(targetDir);
        
        // Create a memory file with a specific timestamp
        var memoryDir = Path.Combine(sourceDir, "workspace", "memory");
        Directory.CreateDirectory(memoryDir);
        var sourcePath = Path.Combine(memoryDir, "2024-01-01.md");
        File.WriteAllText(sourcePath, "# Test");
        var originalTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourcePath, originalTime);

        try
        {
            var migrator = new DataMigrator();
            await migrator.MigrateAsync(sourceDir, targetDir, new MigrationOptions { PreserveTimestamps = true });

            var targetPath = Path.Combine(targetDir, "workspace", "memory", "2024-01-01.md");
            File.GetLastWriteTimeUtc(targetPath).Should().Be(originalTime);
        }
        finally
        {
            Directory.Delete(sourceDir, true);
            Directory.Delete(targetDir, true);
        }
    }

    [Fact]
    public async Task MigrateAsync_DryRun_DoesNotModify()
    {
        var sourceDir = CreateOpenClawLayout();
        var targetDir = Path.Combine(Path.GetTempPath(), $"target-{Guid.NewGuid()}");
        Directory.CreateDirectory(targetDir);
        
        var memoryDir = Path.Combine(sourceDir, "workspace", "memory");
        Directory.CreateDirectory(memoryDir);
        File.WriteAllText(Path.Combine(memoryDir, "test.md"), "# Test");

        try
        {
            var migrator = new DataMigrator();
            var result = await migrator.MigrateAsync(sourceDir, targetDir, new MigrationOptions { DryRun = true });

            result.Success.Should().BeTrue();
            result.MemoriesImported.Should().Be(1);
            
            // Should NOT have created the file
            var targetPath = Path.Combine(targetDir, "workspace", "memory", "test.md");
            File.Exists(targetPath).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(sourceDir, true);
            Directory.Delete(targetDir, true);
        }
    }

    [Fact]
    public async Task MigrateAsync_ConflictSkip_SkipsExisting()
    {
        var sourceDir = CreateOpenClawLayout();
        var targetDir = Path.Combine(Path.GetTempPath(), $"target-{Guid.NewGuid()}");
        
        // Create same file in both source and target
        var sourceMemoryDir = Path.Combine(sourceDir, "workspace", "memory");
        var targetMemoryDir = Path.Combine(targetDir, "workspace", "memory");
        Directory.CreateDirectory(sourceMemoryDir);
        Directory.CreateDirectory(targetMemoryDir);
        
        File.WriteAllText(Path.Combine(sourceMemoryDir, "test.md"), "Source content");
        File.WriteAllText(Path.Combine(targetMemoryDir, "test.md"), "Target content");

        try
        {
            var migrator = new DataMigrator();
            var result = await migrator.MigrateAsync(sourceDir, targetDir, 
                new MigrationOptions { ConflictResolution = ConflictResolution.Skip });

            result.FilesSkipped.Should().Be(1);
            
            // Target file should retain original content
            var content = File.ReadAllText(Path.Combine(targetMemoryDir, "test.md"));
            content.Should().Be("Target content");
        }
        finally
        {
            Directory.Delete(sourceDir, true);
            Directory.Delete(targetDir, true);
        }
    }

    [Fact]
    public async Task MigrateAsync_ConflictOverwrite_ReplacesExisting()
    {
        var sourceDir = CreateOpenClawLayout();
        var targetDir = Path.Combine(Path.GetTempPath(), $"target-{Guid.NewGuid()}");
        
        var sourceMemoryDir = Path.Combine(sourceDir, "workspace", "memory");
        var targetMemoryDir = Path.Combine(targetDir, "workspace", "memory");
        Directory.CreateDirectory(sourceMemoryDir);
        Directory.CreateDirectory(targetMemoryDir);
        
        File.WriteAllText(Path.Combine(sourceMemoryDir, "test.md"), "Source content");
        File.WriteAllText(Path.Combine(targetMemoryDir, "test.md"), "Target content");

        try
        {
            var migrator = new DataMigrator();
            var result = await migrator.MigrateAsync(sourceDir, targetDir, 
                new MigrationOptions { ConflictResolution = ConflictResolution.Overwrite });

            result.FilesOverwritten.Should().Be(1);
            
            // Target file should have source content
            var content = File.ReadAllText(Path.Combine(targetMemoryDir, "test.md"));
            content.Should().Be("Source content");
        }
        finally
        {
            Directory.Delete(sourceDir, true);
            Directory.Delete(targetDir, true);
        }
    }

    [Fact]
    public async Task MigrateAsync_ImportsSessions()
    {
        var sourceDir = CreateOpenClawLayout();
        var targetDir = Path.Combine(Path.GetTempPath(), $"target-{Guid.NewGuid()}");
        Directory.CreateDirectory(targetDir);
        
        // Create session files
        var sessionsDir = Path.Combine(sourceDir, "agents", "main", "sessions");
        Directory.CreateDirectory(sessionsDir);
        File.WriteAllText(Path.Combine(sessionsDir, "session-1.jsonl"), "{\"role\":\"user\"}");

        try
        {
            var migrator = new DataMigrator();
            var result = await migrator.MigrateAsync(sourceDir, targetDir, new MigrationOptions());

            result.SessionsImported.Should().Be(1);
        }
        finally
        {
            Directory.Delete(sourceDir, true);
            Directory.Delete(targetDir, true);
        }
    }

    [Fact]
    public async Task MigrateAsync_ImportsConfig()
    {
        var sourceDir = CreateOpenClawLayout();
        var targetDir = Path.Combine(Path.GetTempPath(), $"target-{Guid.NewGuid()}");
        Directory.CreateDirectory(targetDir);
        
        // Create config file
        File.WriteAllText(Path.Combine(sourceDir, "config.toml"), "workspace_dir = \"/test\"");

        try
        {
            var migrator = new DataMigrator();
            var result = await migrator.MigrateAsync(sourceDir, targetDir, 
                new MigrationOptions { MigrateConfig = true });

            var targetConfig = Path.Combine(targetDir, "config.toml");
            File.Exists(targetConfig).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(sourceDir, true);
            Directory.Delete(targetDir, true);
        }
    }

    [Fact]
    public void MigrationResult_CanBeSerialized()
    {
        var result = new MigrationResult
        {
            Success = true,
            MemoriesImported = 5,
            SessionsImported = 3,
            FilesSkipped = 1,
            FilesOverwritten = 0,
            Errors = new List<string>()
        };

        result.Success.Should().BeTrue();
        result.TotalImported.Should().Be(8);
    }

    // Helper methods to create test directory layouts
    private static string CreateOpenClawLayout()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"openclaw-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "workspace"));
        Directory.CreateDirectory(Path.Combine(dir, "agents"));
        // OpenClaw marker: agents directory with main subdirectory
        Directory.CreateDirectory(Path.Combine(dir, "agents", "main"));
        return dir;
    }

    private static string CreateZeroClawLayout()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"zeroclaw-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        // ZeroClaw marker: .zeroclaw file
        File.WriteAllText(Path.Combine(dir, ".zeroclaw"), "version=1");
        Directory.CreateDirectory(Path.Combine(dir, "data"));
        return dir;
    }

    private static string CreatePicoClawLayout()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"picoclaw-{Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        // PicoClaw marker: picoclaw.json config
        File.WriteAllText(Path.Combine(dir, "picoclaw.json"), "{}");
        return dir;
    }
}
