using Cronos;

namespace ClawSharp.Infrastructure.Cron;

/// <summary>
/// Schedule based on a standard cron expression (5 fields).
/// Supports minute, hour, day of month, month, and day of week.
/// </summary>
public sealed class CronScheduleExpr : ISchedule
{
    private readonly CronExpression _expression;
    private readonly string _expressionString;
    private readonly TimeZoneInfo _timeZone;

    public ScheduleKind Kind => ScheduleKind.Cron;

    /// <summary>The raw cron expression string.</summary>
    public string Expression => _expressionString;

    /// <summary>
    /// Creates a cron schedule from the given expression.
    /// </summary>
    /// <param name="expression">Cron expression (5 fields: minute hour day-of-month month day-of-week).</param>
    /// <param name="timeZone">Optional timezone for evaluation. Defaults to UTC.</param>
    /// <exception cref="ArgumentException">Thrown if the expression is invalid.</exception>
    public CronScheduleExpr(string expression, TimeZoneInfo? timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(expression);

        try
        {
            _expression = CronExpression.Parse(expression);
        }
        catch (CronFormatException ex)
        {
            throw new ArgumentException($"Invalid cron expression: {expression}", nameof(expression), ex);
        }

        _expressionString = expression;
        _timeZone = timeZone ?? TimeZoneInfo.Utc;
    }

    public bool IsDue(DateTimeOffset now, DateTimeOffset? lastRun)
    {
        if (lastRun is null)
            return true;

        // Get the next occurrence after the last run
        var nextOccurrence = GetNextOccurrence(lastRun.Value);

        // It's due if the next occurrence is at or before now
        return nextOccurrence.HasValue && nextOccurrence.Value <= now;
    }

    public DateTimeOffset? GetNextOccurrence(DateTimeOffset from)
    {
        var next = _expression.GetNextOccurrence(from.UtcDateTime, _timeZone);
        return next.HasValue ? new DateTimeOffset(next.Value, TimeSpan.Zero) : null;
    }
}
