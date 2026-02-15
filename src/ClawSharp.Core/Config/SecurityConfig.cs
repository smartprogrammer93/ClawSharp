namespace ClawSharp.Core.Config;

/// <summary>Security and sandboxing settings.</summary>
public class SecurityConfig
{
    public bool SandboxEnabled { get; set; } = true;
    public List<string> AllowedCommands { get; set; } = ["ls", "cat", "grep", "find", "echo", "date"];
    public List<string> AllowedPaths { get; set; } = [];
    public string? PairingSecret { get; set; }
}
