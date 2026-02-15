namespace ClawSharp.Core.Channels;

/// <summary>
/// Represents the status of a channel.
/// </summary>
public sealed class ChannelStatus
{
    public required string Name { get; init; }
    public required bool IsRunning { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset? LastStarted { get; set; }
}
