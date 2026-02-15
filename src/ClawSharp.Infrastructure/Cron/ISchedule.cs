namespace ClawSharp.Infrastructure.Cron;

/// <summary>
/// Types of schedules supported by the cron scheduler.
/// </summary>
public enum ScheduleKind
{
    /// <summary>Standard cron expression (e.g., "0 9 * * *" for 9 AM daily).</summary>
    Cron,

    /// <summary>Run every N milliseconds/seconds/minutes.</summary>
    Every,

    /// <summary>One-shot execution at a specific time.</summary>
    At
}

/// <summary>
/// Base interface for all schedule types.
/// </summary>
public interface ISchedule
{
    /// <summary>The type of schedule.</summary>
    ScheduleKind Kind { get; }

    /// <summary>
    /// Determines if this schedule is currently due for execution.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <param name="lastRun">Time of last execution, or null if never run.</param>
    /// <returns>True if the schedule is due.</returns>
    bool IsDue(DateTimeOffset now, DateTimeOffset? lastRun);

    /// <summary>
    /// Gets the next occurrence after the given time.
    /// </summary>
    /// <param name="from">The starting point for calculation.</param>
    /// <returns>The next scheduled time, or null if no more occurrences.</returns>
    DateTimeOffset? GetNextOccurrence(DateTimeOffset from);

    /// <summary>
    /// Indicates whether this is a one-shot schedule that should be removed after execution.
    /// </summary>
    bool IsOneShot => false;
}
