namespace ClawSharp.Core.Config;

/// <summary>Heartbeat polling settings.</summary>
public class HeartbeatConfig
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 1800;
    public string Prompt { get; set; } = "Read HEARTBEAT.md if it exists. If nothing needs attention, reply HEARTBEAT_OK.";
}
