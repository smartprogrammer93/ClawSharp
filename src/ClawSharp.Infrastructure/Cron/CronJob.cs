namespace ClawSharp.Infrastructure.Cron;

/// <summary>
/// Represents a scheduled job with a name, schedule, and payload.
/// </summary>
public sealed class CronJob
{
    /// <summary>Unique name of the job.</summary>
    public required string Name { get; set; }

    /// <summary>The schedule that determines when the job fires.</summary>
    public required ISchedule Schedule { get; set; }

    /// <summary>Optional payload data for the job (e.g., JSON, message text).</summary>
    public string? Payload { get; set; }

    /// <summary>Optional session target for agent execution.</summary>
    public string? SessionTarget { get; set; }

    /// <summary>When the job was last executed.</summary>
    public DateTimeOffset? LastRun { get; set; }

    /// <summary>When the job was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Whether the job is currently enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Optional metadata for the job.</summary>
    public Dictionary<string, string>? Metadata { get; set; }
}
