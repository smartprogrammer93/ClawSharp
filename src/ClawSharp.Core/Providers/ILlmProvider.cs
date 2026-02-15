namespace ClawSharp.Core.Providers;

/// <summary>
/// Abstraction for any LLM provider (OpenAI, Anthropic, Ollama, etc.)
/// </summary>
public interface ILlmProvider
{
    /// <summary>Provider identifier (e.g., "openai", "anthropic")</summary>
    string Name { get; }

    /// <summary>Whether this provider is currently configured and reachable</summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>List available models for this provider</summary>
    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default);

    /// <summary>Send a completion request (non-streaming)</summary>
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default);

    /// <summary>Send a completion request with streaming</summary>
    IAsyncEnumerable<LlmStreamChunk> StreamAsync(LlmRequest request, CancellationToken ct = default);
}
