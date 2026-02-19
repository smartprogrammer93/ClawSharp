using FluentAssertions;
using ClawSharp.Infrastructure.Cron;
using Xunit;

namespace ClawSharp.Infrastructure.Tests.Cron;

public class CronScheduleExprTests
{
    [Fact]
    public void Constructor_ValidExpression_DoesNotThrow()
    {
        var act = () => new CronScheduleExpr("*/5 * * * *");
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_InvalidExpression_Throws()
    {
        var act = () => new CronScheduleExpr("invalid");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsDue_WhenTimeMatches_ReturnsTrue()
    {
        var schedule = new CronScheduleExpr("*/5 * * * *"); // every 5 minutes
        var lastRun = DateTimeOffset.UtcNow.AddMinutes(-6);
        schedule.IsDue(DateTimeOffset.UtcNow, lastRun).Should().BeTrue();
    }

    [Fact]
    public void IsDue_WhenRecentlyRun_ReturnsFalse()
    {
        var schedule = new CronScheduleExpr("*/5 * * * *");
        // Use a fixed, deterministic time in the middle of a 5-minute interval
        // to avoid flakiness when the test runs near a boundary
        var now = new DateTimeOffset(2024, 1, 15, 10, 2, 30, TimeSpan.Zero);  // 10:02:30
        var lastRun = now.AddSeconds(-30);                                       // 10:02:00
        // Next occurrence after 10:02:00 = 10:05:00, which is NOT <= 10:02:30
        schedule.IsDue(now, lastRun).Should().BeFalse();
    }

    [Fact]
    public void IsDue_WhenNeverRun_ReturnsTrue()
    {
        var schedule = new CronScheduleExpr("*/5 * * * *");
        schedule.IsDue(DateTimeOffset.UtcNow, null).Should().BeTrue();
    }

    [Fact]
    public void GetNextOccurrence_ReturnsCorrectTime()
    {
        var schedule = new CronScheduleExpr("0 12 * * *"); // every day at noon
        var from = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var next = schedule.GetNextOccurrence(from);
        
        next.Should().NotBeNull();
        next!.Value.Hour.Should().Be(12);
        next.Value.Minute.Should().Be(0);
    }

    [Fact]
    public void Kind_ReturnsCron()
    {
        var schedule = new CronScheduleExpr("* * * * *");
        schedule.Kind.Should().Be(ScheduleKind.Cron);
    }
}

public class EveryScheduleTests
{
    [Fact]
    public void Constructor_ValidInterval_DoesNotThrow()
    {
        var act = () => new EverySchedule(TimeSpan.FromMinutes(5));
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_ZeroInterval_Throws()
    {
        var act = () => new EverySchedule(TimeSpan.Zero);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NegativeInterval_Throws()
    {
        var act = () => new EverySchedule(TimeSpan.FromMinutes(-5));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsDue_WhenIntervalElapsed_ReturnsTrue()
    {
        var schedule = new EverySchedule(TimeSpan.FromMinutes(5));
        var lastRun = DateTimeOffset.UtcNow.AddMinutes(-6);
        schedule.IsDue(DateTimeOffset.UtcNow, lastRun).Should().BeTrue();
    }

    [Fact]
    public void IsDue_WhenIntervalNotElapsed_ReturnsFalse()
    {
        var schedule = new EverySchedule(TimeSpan.FromMinutes(5));
        var lastRun = DateTimeOffset.UtcNow.AddMinutes(-2);
        schedule.IsDue(DateTimeOffset.UtcNow, lastRun).Should().BeFalse();
    }

    [Fact]
    public void IsDue_WhenNeverRun_ReturnsTrue()
    {
        var schedule = new EverySchedule(TimeSpan.FromMinutes(5));
        schedule.IsDue(DateTimeOffset.UtcNow, null).Should().BeTrue();
    }

    [Fact]
    public void GetNextOccurrence_ReturnsIntervalAfterNow()
    {
        var interval = TimeSpan.FromHours(1);
        var schedule = new EverySchedule(interval);
        var now = DateTimeOffset.UtcNow;
        var next = schedule.GetNextOccurrence(now);

        next.Should().NotBeNull();
        (next!.Value - now).Should().BeCloseTo(interval, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Kind_ReturnsEvery()
    {
        var schedule = new EverySchedule(TimeSpan.FromMinutes(5));
        schedule.Kind.Should().Be(ScheduleKind.Every);
    }
}

public class AtScheduleTests
{
    [Fact]
    public void IsDue_WhenTimeReached_ReturnsTrue()
    {
        var schedule = new AtSchedule(DateTimeOffset.UtcNow.AddMinutes(-1));
        schedule.IsDue(DateTimeOffset.UtcNow, null).Should().BeTrue();
    }

    [Fact]
    public void IsDue_WhenAlreadyFired_ReturnsFalse()
    {
        var schedule = new AtSchedule(DateTimeOffset.UtcNow.AddMinutes(-1));
        schedule.IsDue(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsDue_WhenTimeFuture_ReturnsFalse()
    {
        var schedule = new AtSchedule(DateTimeOffset.UtcNow.AddHours(1));
        schedule.IsDue(DateTimeOffset.UtcNow, null).Should().BeFalse();
    }

    [Fact]
    public void GetNextOccurrence_ReturnsTargetTime()
    {
        var targetTime = DateTimeOffset.UtcNow.AddHours(2);
        var schedule = new AtSchedule(targetTime);
        var next = schedule.GetNextOccurrence(DateTimeOffset.UtcNow);

        next.Should().Be(targetTime);
    }

    [Fact]
    public void GetNextOccurrence_AfterTarget_ReturnsNull()
    {
        var targetTime = DateTimeOffset.UtcNow.AddHours(-1);
        var schedule = new AtSchedule(targetTime);
        var next = schedule.GetNextOccurrence(DateTimeOffset.UtcNow);

        next.Should().BeNull();
    }

    [Fact]
    public void Kind_ReturnsAt()
    {
        var schedule = new AtSchedule(DateTimeOffset.UtcNow);
        schedule.Kind.Should().Be(ScheduleKind.At);
    }

    [Fact]
    public void IsOneShot_ReturnsTrue()
    {
        var schedule = new AtSchedule(DateTimeOffset.UtcNow);
        schedule.IsOneShot.Should().BeTrue();
    }
}
