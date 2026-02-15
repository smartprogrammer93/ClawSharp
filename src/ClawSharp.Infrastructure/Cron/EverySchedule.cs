namespace ClawSharp.Infrastructure.Cron;

/// <summary>
/// Schedule that repeats at a fixed interval.
/// </summary>
public sealed class EverySchedule : ISchedule
{
    private readonly TimeSpan _interval;

    public ScheduleKind Kind => ScheduleKind.Every;

    /// <summary>The interval between executions.</summary>
    public TimeSpan Interval => _interval;

    /// <summary>
    /// Creates an interval-based schedule.
    /// </summary>
    /// <param name="interval">The interval between executions. Must be positive.</param>
    /// <exception cref="ArgumentException">Thrown if interval is zero or negative.</exception>
    public EverySchedule(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentException("Interval must be positive", nameof(interval));

        _interval = interval;
    }

    /// <summary>
    /// Creates an interval-based schedule from milliseconds.
    /// </summary>
    public static EverySchedule FromMilliseconds(long ms) => new(TimeSpan.FromMilliseconds(ms));

    /// <summary>
    /// Creates an interval-based schedule from seconds.
    /// </summary>
    public static EverySchedule FromSeconds(double seconds) => new(TimeSpan.FromSeconds(seconds));

    /// <summary>
    /// Creates an interval-based schedule from minutes.
    /// </summary>
    public static EverySchedule FromMinutes(double minutes) => new(TimeSpan.FromMinutes(minutes));

    /// <summary>
    /// Creates an interval-based schedule from hours.
    /// </summary>
    public static EverySchedule FromHours(double hours) => new(TimeSpan.FromHours(hours));

    public bool IsDue(DateTimeOffset now, DateTimeOffset? lastRun)
    {
        if (lastRun is null)
            return true;

        return now >= lastRun.Value + _interval;
    }

    public DateTimeOffset? GetNextOccurrence(DateTimeOffset from)
    {
        return from + _interval;
    }
}
