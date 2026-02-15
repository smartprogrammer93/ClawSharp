namespace ClawSharp.UI.Services;

/// <summary>
/// Application state for the Blazor UI.
/// </summary>
public class AppState
{
    // Chat state
    public List<ChatMessage> Messages { get; set; } = [];
    public bool IsProcessing { get; set; }
    public string? CurrentError { get; set; }
    
    // Dashboard state
    public SystemStatus? SystemStatus { get; set; }
    public bool IsLoading { get; set; }
}

/// <summary>
/// A chat message.
/// </summary>
public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Role { get; set; } = "user"; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<ToolExecution> ToolExecutions { get; set; } = [];
}

/// <summary>
/// Tool execution within a message.
/// </summary>
public class ToolExecution
{
    public string ToolName { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string? Result { get; set; }
    public bool IsCompleted { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// System status information.
/// </summary>
public class SystemStatus
{
    public string Version { get; set; } = "1.0.0";
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public TimeSpan Uptime => DateTime.UtcNow - StartTime;
    public List<ProviderStatus> Providers { get; set; } = [];
    public List<ChannelStatus> Channels { get; set; } = [];
    public MemoryStats Memory { get; set; } = new();
}

/// <summary>
/// Provider status information.
/// </summary>
public class ProviderStatus
{
    public string Name { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public List<string> Models { get; set; } = [];
    public double? LatencyMs { get; set; }
}

/// <summary>
/// Channel status information.
/// </summary>
public class ChannelStatus
{
    public string Name { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public string? Error { get; set; }
    public DateTime? LastMessageAt { get; set; }
}

/// <summary>
/// Memory statistics.
/// </summary>
public class MemoryStats
{
    public int TotalEntries { get; set; }
    public long TotalSizeBytes { get; set; }
}
