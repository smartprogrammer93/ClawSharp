namespace ClawSharp.Core.Config;

/// <summary>LLM provider configurations.</summary>
public class ProvidersConfig
{
    public ProviderEntry? Openai { get; set; }
    public ProviderEntry? Anthropic { get; set; }
    public ProviderEntry? OpenRouter { get; set; }
    public ProviderEntry? Ollama { get; set; }
    public ProviderEntry? MiniMax { get; set; }
    public List<ProviderEntry> Compatible { get; set; } = [];
}

/// <summary>A single provider's connection settings.</summary>
public class ProviderEntry
{
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public string? DefaultModel { get; set; }
    public string? GroupId { get; set; }
    
    /// <summary>
    /// Authentication mode: "api_key" (default) or "oauth".
    /// When set to "oauth", the provider will load tokens from auth-profiles.json.
    /// </summary>
    public string? AuthMode { get; set; }
    
    /// <summary>
    /// Path to the auth-profiles.json file (defaults to ~/.openclaw/agents/main/agent/auth-profiles.json).
    /// </summary>
    public string? AuthProfilesPath { get; set; }
    
    /// <summary>
    /// The profile ID to use from auth-profiles.json (e.g., "anthropic:default").
    /// </summary>
    public string? AuthProfileId { get; set; }
}
