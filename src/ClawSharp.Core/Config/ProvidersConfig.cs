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
}
