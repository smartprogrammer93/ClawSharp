using ClawSharp.Core.Config;
using ClawSharp.Core.Memory;
using ClawSharp.Core.Providers;
using ClawSharp.Core.Tools;

namespace ClawSharp.Agent;

/// <summary>
/// Builds the full prompt context: system prompt + memory + tools + history.
/// </summary>
public class ContextBuilder
{
    private readonly IMemoryStore _memoryStore;
    private readonly IToolRegistry _toolRegistry;
    private readonly ClawSharpConfig _config;

    public ContextBuilder(
        IMemoryStore memoryStore,
        IToolRegistry toolRegistry,
        ClawSharpConfig config)
    {
        _memoryStore = memoryStore;
        _toolRegistry = toolRegistry;
        _config = config;
    }

    /// <summary>
    /// Builds the full system prompt including identity, memories, and tools.
    /// </summary>
    public async Task<string> BuildSystemPromptAsync(CancellationToken ct = default)
    {
        var sections = new List<string>();

        // Identity section
        sections.Add($"# Identity\nYou are ClawSharp, an AI assistant running on .NET.");
        sections.Add($"Current time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

        // Memories section
        var memories = await _memoryStore.ListAsync(MemoryCategory.Core, limit: 20, ct: ct);
        if (memories.Count > 0)
        {
            sections.Add("\n# Relevant Memories");
            foreach (var memory in memories)
            {
                sections.Add($"- [{memory.Key}]: {memory.Content}");
            }
        }

        // Tools section
        var tools = _toolRegistry.GetSpecifications();
        if (tools.Count > 0)
        {
            sections.Add("\n# Available Tools\nYou can use the following tools:");
            foreach (var tool in tools)
            {
                sections.Add($"## {tool.Name}");
                sections.Add(tool.Description ?? "No description");
            }
        }

        return string.Join("\n\n", sections);
    }

    /// <summary>
    /// Trims conversation history to fit within token budget.
    /// Uses a simple estimate: 4 characters ≈ 1 token.
    /// </summary>
    public List<LlmMessage> TrimHistory(List<LlmMessage> history, int maxTokens)
    {
        if (history.Count == 0)
            return [];

        // Estimate tokens as characters / 4
        var estimatedTokens = history.Sum(m => (m.Content?.Length ?? 0) / 4);
        
        if (estimatedTokens <= maxTokens)
            return history.ToList();

        // Need to trim - keep the most recent messages
        // Aim for roughly maxTokens
        var trimmed = new List<LlmMessage>();
        var currentTokens = 0;

        // Iterate from the most recent messages backwards
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var msg = history[i];
            var msgTokens = (msg.Content?.Length ?? 0) / 4;
            
            if (currentTokens + msgTokens > maxTokens)
                break;
                
            trimmed.Insert(0, msg);
            currentTokens += msgTokens;
        }

        return trimmed;
    }

    /// <summary>
    /// Builds the full context for a request including system prompt, context, and messages.
    /// </summary>
    public async Task<List<LlmMessage>> BuildContextAsync(
        List<LlmMessage> conversationHistory,
        CancellationToken ct = default)
    {
        var systemPrompt = await BuildSystemPromptAsync(ct);
        
        // Trim history to fit budget (estimate 8000 tokens for system prompt)
        var trimmedHistory = TrimHistory(conversationHistory, _config.MaxContextTokens - 8000);
        
        // Add system message at the beginning
        var messages = new List<LlmMessage>
        {
            new("system", systemPrompt)
        };
        
        messages.AddRange(trimmedHistory);
        
        return messages;
    }
}
