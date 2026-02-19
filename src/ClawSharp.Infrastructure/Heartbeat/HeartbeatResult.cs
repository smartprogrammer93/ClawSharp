namespace ClawSharp.Infrastructure.Heartbeat;

/// <summary>
/// Result of a heartbeat execution.
/// </summary>
public sealed record HeartbeatResult
{
    /// <summary>
    /// Whether the heartbeat was skipped (e.g., disabled in config).
    /// </summary>
    public bool Skipped { get; init; }

    /// <summary>
    /// The response from the agent, if any.
    /// </summary>
    public string? Response { get; init; }

    /// <summary>
    /// Whether the response requires delivery to a channel.
    /// False if the response is "HEARTBEAT_OK" or similar.
    /// </summary>
    public bool RequiresDelivery { get; init; }

    /// <summary>
    /// Error message if the heartbeat failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// The time when this heartbeat was executed.
    /// </summary>
    public DateTimeOffset ExecutedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a skipped result.
    /// </summary>
    public static HeartbeatResult CreateSkipped() => new()
    {
        Skipped = true,
        RequiresDelivery = false
    };

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static HeartbeatResult CreateSuccess(string response, bool requiresDelivery) => new()
    {
        Skipped = false,
        Response = response,
        RequiresDelivery = requiresDelivery
    };

    /// <summary>
    /// Creates an error result.
    /// </summary>
    public static HeartbeatResult CreateError(string error) => new()
    {
        Skipped = false,
        Error = error,
        RequiresDelivery = false
    };
}
