namespace ClawSharp.Infrastructure.Cron;

/// <summary>
/// One-shot schedule that fires at a specific time.
/// Should be deleted after execution.
/// </summary>
public sealed class AtSchedule : ISchedule
{
    private readonly DateTimeOffset _targetTime;

    public ScheduleKind Kind => ScheduleKind.At;

    /// <summary>Indicates this is a one-shot schedule.</summary>
    public bool IsOneShot => true;

    /// <summary>The target execution time.</summary>
    public DateTimeOffset TargetTime => _targetTime;

    /// <summary>
    /// Creates a one-shot schedule for the specified time.
    /// </summary>
    /// <param name="targetTime">The time at which to fire.</param>
    public AtSchedule(DateTimeOffset targetTime)
    {
        _targetTime = targetTime;
    }

    /// <summary>
    /// Creates a one-shot schedule from a Unix timestamp in milliseconds.
    /// </summary>
    public static AtSchedule FromUnixMilliseconds(long unixMs)
    {
        return new AtSchedule(DateTimeOffset.FromUnixTimeMilliseconds(unixMs));
    }

    public bool IsDue(DateTimeOffset now, DateTimeOffset? lastRun)
    {
        // If already fired, never due again
        if (lastRun.HasValue)
            return false;

        // Due if target time has passed
        return now >= _targetTime;
    }

    public DateTimeOffset? GetNextOccurrence(DateTimeOffset from)
    {
        // If target is in the future, return it
        if (_targetTime > from)
            return _targetTime;

        // Target has passed, no more occurrences
        return null;
    }
}
