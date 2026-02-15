namespace ClawSharp.Core.Config;

/// <summary>HTTP gateway settings.</summary>
public class GatewayConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8080;
    public bool EnableUi { get; set; } = true;
    public string? ApiKey { get; set; }
}
