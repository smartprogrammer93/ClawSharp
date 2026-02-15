namespace ClawSharp.Core.Config;

/// <summary>Root configuration for ClawSharp.</summary>
public class ClawSharpConfig
{
    public string WorkspaceDir { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clawsharp", "workspace");

    public string DataDir { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clawsharp");

    public string? DefaultProvider { get; set; }
    public string? DefaultModel { get; set; }
    public double DefaultTemperature { get; set; } = 0.7;
    public int MaxContextTokens { get; set; } = 128_000;

    public ProvidersConfig Providers { get; set; } = new();
    public MemoryConfig Memory { get; set; } = new();
    public GatewayConfig Gateway { get; set; } = new();
    public ChannelsConfig Channels { get; set; } = new();
    public SecurityConfig Security { get; set; } = new();
    public HeartbeatConfig Heartbeat { get; set; } = new();
    public TunnelConfig Tunnel { get; set; } = new();
}
