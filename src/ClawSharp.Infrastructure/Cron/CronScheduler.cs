using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace ClawSharp.Infrastructure.Cron;

/// <summary>
/// Persistent cron scheduler using SQLite for storage.
/// Thread-safe for concurrent access.
/// </summary>
public sealed class CronScheduler : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;
    private readonly object _lock = new();
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public CronScheduler(string dataDir)
    {
        ArgumentNullException.ThrowIfNull(dataDir);

        Directory.CreateDirectory(dataDir);
        _dbPath = Path.Combine(dataDir, "cron.db");

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        _connection = new SqliteConnection(builder.ConnectionString);
        _connection.Open();

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS jobs (
                name TEXT PRIMARY KEY,
                schedule_kind TEXT NOT NULL,
                schedule_data TEXT NOT NULL,
                payload TEXT,
                session_target TEXT,
                last_run TEXT,
                created_at TEXT NOT NULL,
                enabled INTEGER NOT NULL DEFAULT 1,
                metadata TEXT
            );
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Adds or replaces a job.
    /// </summary>
    public void AddJob(CronJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(job.Name);
        ArgumentNullException.ThrowIfNull(job.Schedule);

        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT OR REPLACE INTO jobs (name, schedule_kind, schedule_data, payload, session_target, last_run, created_at, enabled, metadata)
                VALUES (@name, @scheduleKind, @scheduleData, @payload, @sessionTarget, @lastRun, @createdAt, @enabled, @metadata)
                """;

            cmd.Parameters.AddWithValue("@name", job.Name);
            cmd.Parameters.AddWithValue("@scheduleKind", job.Schedule.Kind.ToString());
            cmd.Parameters.AddWithValue("@scheduleData", SerializeSchedule(job.Schedule));
            cmd.Parameters.AddWithValue("@payload", job.Payload ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@sessionTarget", job.SessionTarget ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@lastRun", job.LastRun?.ToString("O") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@createdAt", job.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@enabled", job.Enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("@metadata", job.Metadata != null ? JsonSerializer.Serialize(job.Metadata) : DBNull.Value);

            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Removes a job by name.
    /// </summary>
    public void RemoveJob(string name)
    {
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM jobs WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", name);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Gets a job by name.
    /// </summary>
    public CronJob? GetJob(string name)
    {
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM jobs WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", name);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return ReadJob(reader);
            }
            return null;
        }
    }

    /// <summary>
    /// Lists all jobs.
    /// </summary>
    public IReadOnlyList<CronJob> ListJobs()
    {
        lock (_lock)
        {
            var jobs = new List<CronJob>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM jobs";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                jobs.Add(ReadJob(reader));
            }
            return jobs;
        }
    }

    /// <summary>
    /// Gets all jobs that are currently due for execution.
    /// </summary>
    public IReadOnlyList<CronJob> GetDueJobs()
    {
        var now = DateTimeOffset.UtcNow;
        var allJobs = ListJobs();
        return allJobs.Where(j => j.Enabled && j.Schedule.IsDue(now, j.LastRun)).ToList();
    }

    /// <summary>
    /// Updates a job's last run time and optionally deletes one-shot jobs.
    /// </summary>
    public void MarkJobRan(string name, DateTimeOffset runTime, bool deleteOneShot = false)
    {
        lock (_lock)
        {
            if (deleteOneShot)
            {
                var job = GetJob(name);
                if (job?.Schedule.IsOneShot == true)
                {
                    RemoveJob(name);
                    return;
                }
            }

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "UPDATE jobs SET last_run = @lastRun WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@lastRun", runTime.ToString("O"));
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Updates a job using a transformation function.
    /// </summary>
    public void UpdateJob(string name, Func<CronJob, CronJob> transform)
    {
        lock (_lock)
        {
            var job = GetJob(name);
            if (job == null) return;

            var updated = transform(job);
            AddJob(updated);
        }
    }

    /// <summary>
    /// Removes all jobs.
    /// </summary>
    public void ClearAllJobs()
    {
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM jobs";
            cmd.ExecuteNonQuery();
        }
    }

    private static CronJob ReadJob(SqliteDataReader reader)
    {
        var scheduleKind = Enum.Parse<ScheduleKind>(reader.GetString(reader.GetOrdinal("schedule_kind")));
        var scheduleData = reader.GetString(reader.GetOrdinal("schedule_data"));
        var schedule = DeserializeSchedule(scheduleKind, scheduleData);

        var lastRunOrdinal = reader.GetOrdinal("last_run");
        DateTimeOffset? lastRun = reader.IsDBNull(lastRunOrdinal) ? null : DateTimeOffset.Parse(reader.GetString(lastRunOrdinal));

        var metadataOrdinal = reader.GetOrdinal("metadata");
        Dictionary<string, string>? metadata = reader.IsDBNull(metadataOrdinal)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(metadataOrdinal));

        return new CronJob
        {
            Name = reader.GetString(reader.GetOrdinal("name")),
            Schedule = schedule,
            Payload = reader.IsDBNull(reader.GetOrdinal("payload")) ? null : reader.GetString(reader.GetOrdinal("payload")),
            SessionTarget = reader.IsDBNull(reader.GetOrdinal("session_target")) ? null : reader.GetString(reader.GetOrdinal("session_target")),
            LastRun = lastRun,
            CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
            Enabled = reader.GetInt32(reader.GetOrdinal("enabled")) == 1,
            Metadata = metadata
        };
    }

    private static string SerializeSchedule(ISchedule schedule)
    {
        return schedule switch
        {
            CronScheduleExpr cron => cron.Expression,
            EverySchedule every => every.Interval.TotalMilliseconds.ToString(),
            AtSchedule at => at.TargetTime.ToString("O"),
            _ => throw new ArgumentException($"Unknown schedule type: {schedule.GetType()}")
        };
    }

    private static ISchedule DeserializeSchedule(ScheduleKind kind, string data)
    {
        return kind switch
        {
            ScheduleKind.Cron => new CronScheduleExpr(data),
            ScheduleKind.Every => new EverySchedule(TimeSpan.FromMilliseconds(double.Parse(data))),
            ScheduleKind.At => new AtSchedule(DateTimeOffset.Parse(data)),
            _ => throw new ArgumentException($"Unknown schedule kind: {kind}")
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _connection.Close();
        _connection.Dispose();
    }
}
