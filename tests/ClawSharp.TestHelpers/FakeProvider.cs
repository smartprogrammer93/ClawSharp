using System.Runtime.CompilerServices;
using ClawSharp.Core.Providers;

namespace ClawSharp.TestHelpers;

/// <summary>
/// A fake ILlmProvider for testing the agent loop without real API calls.
/// </summary>
public sealed class FakeProvider : ILlmProvider
{
    private readonly Queue<LlmResponse> _responses = new();
    private readonly List<LlmRequest> _receivedRequests = [];

    public string Name => "fake";

    /// <summary>All requests received by this provider.</summary>
    public IReadOnlyList<LlmRequest> ReceivedRequests => _receivedRequests;

    /// <summary>Enqueue a response to return on the next CompleteAsync call.</summary>
    public FakeProvider EnqueueResponse(string content, string finishReason = "stop")
    {
        _responses.Enqueue(new LlmResponse(content, [], finishReason, new UsageInfo(10, 20, 30)));
        return this;
    }

    /// <summary>Enqueue a response with tool calls.</summary>
    public FakeProvider EnqueueToolCallResponse(IReadOnlyList<ToolCallRequest> toolCalls)
    {
        _responses.Enqueue(new LlmResponse("", toolCalls, "tool_calls", new UsageInfo(10, 20, 30)));
        return this;
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) =>
        Task.FromResult(true);

    public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(["fake-model", "fake-model-large"]);

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        _receivedRequests.Add(request);

        if (_responses.Count == 0)
            return Task.FromResult(new LlmResponse("Default fake response", [], "stop", new UsageInfo(5, 10, 15)));

        return Task.FromResult(_responses.Dequeue());
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(LlmRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var response = await CompleteAsync(request, ct);

        // Simulate streaming by yielding content in chunks
        foreach (var word in response.Content.Split(' '))
        {
            yield return new LlmStreamChunk(word + " ", null, null, null);
        }

        yield return new LlmStreamChunk(null, null, response.FinishReason, response.Usage);
    }
}
