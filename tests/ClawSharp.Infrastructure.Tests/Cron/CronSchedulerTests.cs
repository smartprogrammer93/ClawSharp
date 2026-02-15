using FluentAssertions;
using ClawSharp.Infrastructure.Cron;
using ClawSharp.TestHelpers;
using Xunit;
using TestHelperFactory = ClawSharp.TestHelpers.TestHelpers;

namespace ClawSharp.Infrastructure.Tests.Cron;

public class CronSchedulerTests : IDisposable
{
    private readonly TempDirectory _tempDir;
    private readonly CronScheduler _scheduler;

    public CronSchedulerTests()
    {
        _tempDir = TestHelperFactory.CreateTempDirectory();
        _scheduler = new CronScheduler(_tempDir.Path);
    }

    public void Dispose()
    {
        _scheduler.Dispose();
        _tempDir.Dispose();
    }

    [Fact]
    public void AddJob_IncreasesJobCount()
    {
        _scheduler.AddJob(new CronJob
        {
            Name = "test",
            Schedule = new EverySchedule(TimeSpan.FromHours(1)),
            Payload = "hello"
        });

        _scheduler.ListJobs().Should().HaveCount(1);
    }

    [Fact]
    public void AddJob_DuplicateName_ReplacesExisting()
    {
        _scheduler.AddJob(new CronJob
        {
            Name = "test",
            Schedule = new EverySchedule(TimeSpan.FromHours(1)),
            Payload = "first"
        });

        _scheduler.AddJob(new CronJob
        {
            Name = "test",
            Schedule = new EverySchedule(TimeSpan.FromHours(2)),
            Payload = "second"
        });

        _scheduler.ListJobs().Should().HaveCount(1);
        _scheduler.GetJob("test")!.Payload.Should().Be("second");
    }

    [Fact]
    public void RemoveJob_DecreasesJobCount()
    {
        _scheduler.AddJob(new CronJob
        {
            Name = "test",
            Schedule = new EverySchedule(TimeSpan.FromHours(1)),
            Payload = "hello"
        });

        _scheduler.RemoveJob("test");
        _scheduler.ListJobs().Should().BeEmpty();
    }

    [Fact]
    public void RemoveJob_NonExistent_DoesNotThrow()
    {
        var act = () => _scheduler.RemoveJob("nonexistent");
        act.Should().NotThrow();
    }

    [Fact]
    public void GetJob_Existing_ReturnsJob()
    {
        var job = new CronJob
        {
            Name = "test",
            Schedule = new EverySchedule(TimeSpan.FromHours(1)),
            Payload = "hello"
        };
        _scheduler.AddJob(job);

        var retrieved = _scheduler.GetJob("test");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("test");
        retrieved.Payload.Should().Be("hello");
    }

    [Fact]
    public void GetJob_NonExistent_ReturnsNull()
    {
        _scheduler.GetJob("nonexistent").Should().BeNull();
    }

    [Fact]
    public void ListJobs_ReturnsAllJobs()
    {
        _scheduler.AddJob(new CronJob { Name = "job1", Schedule = new EverySchedule(TimeSpan.FromHours(1)) });
        _scheduler.AddJob(new CronJob { Name = "job2", Schedule = new EverySchedule(TimeSpan.FromHours(2)) });
        _scheduler.AddJob(new CronJob { Name = "job3", Schedule = new EverySchedule(TimeSpan.FromHours(3)) });

        var jobs = _scheduler.ListJobs();
        jobs.Should().HaveCount(3);
        jobs.Select(j => j.Name).Should().BeEquivalentTo(["job1", "job2", "job3"]);
    }

    [Fact]
    public void GetDueJobs_ReturnsDueJobs()
    {
        var now = DateTimeOffset.UtcNow;

        // This job should be due immediately
        _scheduler.AddJob(new CronJob
        {
            Name = "due-job",
            Schedule = new EverySchedule(TimeSpan.FromSeconds(1)),
            Payload = "due"
        });

        // This job should not be due (scheduled far in the future)
        _scheduler.AddJob(new CronJob
        {
            Name = "not-due",
            Schedule = new AtSchedule(now.AddHours(10)),
            Payload = "not due"
        });

        var dueJobs = _scheduler.GetDueJobs();
        dueJobs.Should().ContainSingle();
        dueJobs[0].Name.Should().Be("due-job");
    }

    [Fact]
    public void MarkJobRan_UpdatesLastRun()
    {
        _scheduler.AddJob(new CronJob
        {
            Name = "test",
            Schedule = new EverySchedule(TimeSpan.FromHours(1))
        });

        var runTime = DateTimeOffset.UtcNow;
        _scheduler.MarkJobRan("test", runTime);

        var job = _scheduler.GetJob("test");
        job!.LastRun.Should().BeCloseTo(runTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Persistence_LoadsFromDisk()
    {
        _scheduler.AddJob(new CronJob
        {
            Name = "persistent-job",
            Schedule = new EverySchedule(TimeSpan.FromHours(1)),
            Payload = "persistent"
        });

        // Create new scheduler instance
        using var scheduler2 = new CronScheduler(_tempDir.Path);
        var job = scheduler2.GetJob("persistent-job");

        job.Should().NotBeNull();
        job!.Name.Should().Be("persistent-job");
        job.Payload.Should().Be("persistent");
    }

    [Fact]
    public void OneShotJobs_RemovedAfterExecution()
    {
        var targetTime = DateTimeOffset.UtcNow.AddSeconds(-1);
        _scheduler.AddJob(new CronJob
        {
            Name = "oneshot",
            Schedule = new AtSchedule(targetTime),
            Payload = "oneshot"
        });

        var dueJobs = _scheduler.GetDueJobs();
        dueJobs.Should().ContainSingle();

        _scheduler.MarkJobRan("oneshot", DateTimeOffset.UtcNow, deleteOneShot: true);

        _scheduler.GetJob("oneshot").Should().BeNull();
    }

    [Fact]
    public void AddJob_WithCronExpression_Works()
    {
        _scheduler.AddJob(new CronJob
        {
            Name = "cron-job",
            Schedule = new CronScheduleExpr("0 9 * * *"), // 9 AM daily
            Payload = "daily"
        });

        var job = _scheduler.GetJob("cron-job");
        job.Should().NotBeNull();
        job!.Schedule.Kind.Should().Be(ScheduleKind.Cron);
    }

    [Fact]
    public void UpdateJob_ModifiesExistingJob()
    {
        _scheduler.AddJob(new CronJob
        {
            Name = "test",
            Schedule = new EverySchedule(TimeSpan.FromHours(1)),
            Payload = "original"
        });

        _scheduler.UpdateJob("test", job =>
        {
            job.Payload = "updated";
            return job;
        });

        _scheduler.GetJob("test")!.Payload.Should().Be("updated");
    }

    [Fact]
    public void UpdateJob_NonExistent_DoesNothing()
    {
        var act = () => _scheduler.UpdateJob("nonexistent", job => job);
        act.Should().NotThrow();
    }

    [Fact]
    public void ClearAllJobs_RemovesEverything()
    {
        _scheduler.AddJob(new CronJob { Name = "job1", Schedule = new EverySchedule(TimeSpan.FromHours(1)) });
        _scheduler.AddJob(new CronJob { Name = "job2", Schedule = new EverySchedule(TimeSpan.FromHours(2)) });

        _scheduler.ClearAllJobs();

        _scheduler.ListJobs().Should().BeEmpty();
    }
}
