using ClawSharp.Core.Providers;
using System.Text.Json;

namespace ClawSharp.TestHelpers;

/// <summary>
/// Common test fixtures — pre-built objects for use in tests.
/// </summary>
public static class TestFixtures
{
    /// <summary>A simple user message.</summary>
    public static LlmMessage UserMessage(string content = "Hello") =>
        new("user", content);

    /// <summary>A simple assistant message.</summary>
    public static LlmMessage AssistantMessage(string content = "Hi there!") =>
        new("assistant", content);

    /// <summary>A system message.</summary>
    public static LlmMessage SystemMessage(string content = "You are a helpful assistant.") =>
        new("system", content);

    /// <summary>A minimal LlmRequest.</summary>
    public static LlmRequest SimpleRequest(string model = "fake-model", string userMessage = "Hello") =>
        new()
        {
            Model = model,
            Messages = [UserMessage(userMessage)],
            Temperature = 0.0,
        };

    /// <summary>A request with system prompt and tools.</summary>
    public static LlmRequest RequestWithTools(string model = "fake-model") =>
        new()
        {
            Model = model,
            Messages = [SystemMessage(), UserMessage("What's the weather?")],
            Tools = [SampleToolSpec()],
            Temperature = 0.0,
        };

    /// <summary>A sample tool specification.</summary>
    public static ToolSpec SampleToolSpec() =>
        new("get_weather", "Get current weather for a location",
            JsonDocument.Parse("""{"type":"object","properties":{"location":{"type":"string"}},"required":["location"]}""").RootElement);

    /// <summary>A sample tool call request.</summary>
    public static ToolCallRequest SampleToolCall() =>
        new("call_1", "get_weather", """{"location":"London"}""");

    /// <summary>A successful LlmResponse.</summary>
    public static LlmResponse SimpleResponse(string content = "Hello! How can I help?") =>
        new(content, [], "stop", new UsageInfo(10, 20, 30));

    /// <summary>An LlmResponse with tool calls.</summary>
    public static LlmResponse ToolCallResponse() =>
        new("", [SampleToolCall()], "tool_calls", new UsageInfo(15, 5, 20));
}
