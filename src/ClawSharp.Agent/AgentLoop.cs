using System.Text.Json;
using ClawSharp.Core.Channels;
using ClawSharp.Core.Providers;
using ClawSharp.Core.Tools;
using Microsoft.Extensions.Logging;
using ProviderToolSpec = ClawSharp.Core.Providers.ToolSpec;

namespace ClawSharp.Agent;

/// <summary>
/// The core agent loop that orchestrates LLM ↔ tool calling.
/// </summary>
public class AgentLoop
{
    private readonly ILlmProvider _provider;
    private readonly IToolRegistry _tools;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<AgentLoop> _logger;
    private readonly int _maxIterations;

    /// <summary>
    /// Request for running the agent loop.
    /// </summary>
    public record AgentRequest(
        string Model,
        IReadOnlyList<LlmMessage> InitialMessages
    );

    /// <summary>
    /// Result of running the agent loop.
    /// </summary>
    public record AgentResult(
        string Content,
        IReadOnlyList<ToolExecution> ToolExecutions
    );

    /// <summary>
    /// Record of a tool execution.
    /// </summary>
    public record ToolExecution(
        string ToolCallId,
        string ToolName,
        string ArgumentsJson,
        ToolResult Result
    );

    /// <summary>
    /// Event published when a tool starts execution.
    /// </summary>
    public record ToolStartedEvent(
        string ToolCallId,
        string ToolName,
        string ArgumentsJson
    );

    /// <summary>
    /// Event published when a tool completes execution.
    /// </summary>
    public record ToolCompletedEvent(
        string ToolCallId,
        string ToolName,
        ToolResult Result
    );

    /// <summary>
    /// Creates a new AgentLoop.
    /// </summary>
    public AgentLoop(
        ILlmProvider provider,
        IToolRegistry tools,
        IMessageBus messageBus,
        ILogger<AgentLoop> logger,
        int maxIterations = 20)
    {
        _provider = provider;
        _tools = tools;
        _messageBus = messageBus;
        _logger = logger;
        _maxIterations = maxIterations;
    }

    /// <summary>
    /// Run the agent loop with the given request.
    /// </summary>
    public async Task<AgentResult> RunAsync(AgentRequest request, CancellationToken ct = default)
    {
        var messages = new List<LlmMessage>(request.InitialMessages);
        var toolExecutions = new List<ToolExecution>();
        var toolSpecs = _tools.GetSpecifications();

        // Convert from Tools.ToolSpec to Providers.ToolSpec
        var providerToolSpecs = toolSpecs.Count > 0
            ? toolSpecs.Select(t => new ProviderToolSpec(t.Name, t.Description, t.ParametersSchema)).ToList()
            : null;

        for (int iteration = 0; iteration < _maxIterations; iteration++)
        {
            var llmRequest = new LlmRequest
            {
                Model = request.Model,
                Messages = messages,
                Tools = providerToolSpecs
            };

            var response = await _provider.CompleteAsync(llmRequest, ct);

            // If no tool calls, we're done
            if (response.ToolCalls.Count == 0)
            {
                return new AgentResult(response.Content, toolExecutions);
            }

            // Process tool calls
            foreach (var toolCall in response.ToolCalls)
            {
                var (toolMessage, execution) = await ExecuteToolAsync(toolCall, ct);
                toolExecutions.Add(execution);
                messages.Add(toolMessage);
            }

            // Add assistant message with tool calls to history
            messages.Add(new LlmMessage("assistant", response.Content, response.ToolCalls));
        }

        // Max iterations reached
        return new AgentResult(
            $"Maximum iteration limit ({_maxIterations}) reached. Consider increasing the limit or optimizing your tool usage.",
            toolExecutions
        );
    }

    private async Task<(LlmMessage Message, ToolExecution Execution)> ExecuteToolAsync(ToolCallRequest toolCall, CancellationToken ct)
    {
        // Publish started event
        await _messageBus.PublishAsync(new ToolStartedEvent(
            toolCall.Id,
            toolCall.Name,
            toolCall.ArgumentsJson), ct);

        var tool = _tools.Get(toolCall.Name);
        ToolResult result;

        if (tool == null)
        {
            result = new ToolResult(false, string.Empty, $"Tool '{toolCall.Name}' not found");
        }
        else
        {
            try
            {
                var args = JsonSerializer.Deserialize<JsonElement>(toolCall.ArgumentsJson);
                result = await tool.ExecuteAsync(args, ct);
            }
            catch (Exception ex)
            {
                result = new ToolResult(false, string.Empty, $"Error executing tool: {ex.Message}");
            }
        }

        var execution = new ToolExecution(
            toolCall.Id,
            toolCall.Name,
            toolCall.ArgumentsJson,
            result
        );

        // Publish completed event
        await _messageBus.PublishAsync(new ToolCompletedEvent(
            toolCall.Id,
            toolCall.Name,
            result), ct);

        // Return tool result message and execution
        return (
            new LlmMessage(
                "tool",
                result.Success ? result.Output : $"Error: {result.Error}",
                ToolCallId: toolCall.Id,
                Name: toolCall.Name
            ),
            execution
        );
    }
}
