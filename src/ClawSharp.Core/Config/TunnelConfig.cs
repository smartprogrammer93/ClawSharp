namespace ClawSharp.Core.Config;

/// <summary>Tunnel settings for external access.</summary>
public class TunnelConfig
{
    public string? Provider { get; set; }
    public string? Token { get; set; }
    public string? Domain { get; set; }
}
