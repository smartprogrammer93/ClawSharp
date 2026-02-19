namespace ClawSharp.Infrastructure.Migration;

/// <summary>
/// Supported migration source formats.
/// </summary>
public enum MigrationSourceFormat
{
    Unknown,
    OpenClaw,
    ZeroClaw,
    PicoClaw
}

/// <summary>
/// How to handle file conflicts during migration.
/// </summary>
public enum ConflictResolution
{
    Skip,
    Overwrite,
    Rename
}

/// <summary>
/// Options for data migration.
/// </summary>
public class MigrationOptions
{
    /// <summary>
    /// If true, don't actually write files - just report what would be done.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// How to handle conflicts when target files already exist.
    /// </summary>
    public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.Skip;

    /// <summary>
    /// Whether to preserve file timestamps from source.
    /// </summary>
    public bool PreserveTimestamps { get; set; } = true;

    /// <summary>
    /// Whether to migrate config files.
    /// </summary>
    public bool MigrateConfig { get; set; }

    /// <summary>
    /// Whether to migrate memory files.
    /// </summary>
    public bool MigrateMemories { get; set; } = true;

    /// <summary>
    /// Whether to migrate session files.
    /// </summary>
    public bool MigrateSessions { get; set; } = true;
}

/// <summary>
/// Result of a migration operation.
/// </summary>
public class MigrationResult
{
    /// <summary>
    /// Whether the migration completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of memory files imported.
    /// </summary>
    public int MemoriesImported { get; set; }

    /// <summary>
    /// Number of session files imported.
    /// </summary>
    public int SessionsImported { get; set; }

    /// <summary>
    /// Number of files skipped due to conflicts.
    /// </summary>
    public int FilesSkipped { get; set; }

    /// <summary>
    /// Number of files overwritten.
    /// </summary>
    public int FilesOverwritten { get; set; }

    /// <summary>
    /// Any errors encountered during migration.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Total files imported (memories + sessions).
    /// </summary>
    public int TotalImported => MemoriesImported + SessionsImported;
}
