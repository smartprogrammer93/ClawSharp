namespace ClawSharp.Core.Config;

/// <summary>Memory store settings.</summary>
public class MemoryConfig
{
    public string DbPath { get; set; } = "memory.db";
    public bool EnableVectorSearch { get; set; } = true;
    public string? EmbeddingProvider { get; set; }
    public string? EmbeddingModel { get; set; }
}
