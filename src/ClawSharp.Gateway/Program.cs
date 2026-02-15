using ClawSharp.Agent;
using ClawSharp.Channels;
using ClawSharp.Core.Channels;
using ClawSharp.Core.Config;
using ClawSharp.Core.Memory;
using ClawSharp.Core.Providers;
using ClawSharp.Core.Sessions;
using ClawSharp.Core.Tools;
using ClawSharp.Gateway;
using ClawSharp.Gateway.Endpoints;
using ClawSharp.Gateway.Hubs;
using ClawSharp.Infrastructure;
using ClawSharp.Infrastructure.Messaging;
using ClawSharp.Memory;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// Add ClawSharp services
var config = new ClawSharpConfig
{
    DataDir = Path.Combine(builder.Environment.ContentRootPath, "data"),
    WorkspaceDir = Path.Combine(builder.Environment.ContentRootPath, "workspace"),
    DefaultProvider = "test",
    DefaultModel = "test-model"
};
builder.Services.AddSingleton(config);

// Add in-memory message bus
builder.Services.AddSingleton<IMessageBus, InProcessMessageBus>();

// Add session manager (in-memory for now)
builder.Services.AddSingleton<ISessionManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SqliteSessionManager>>();
    return new SqliteSessionManager(":memory:");
});

// Add a mock LLM provider for testing
builder.Services.AddSingleton<ILlmProvider>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<TestLlmProvider>>();
    return new TestLlmProvider(logger);
});

// Add tool registry (empty for now)
builder.Services.AddSingleton<IToolRegistry>(sp =>
{
    return new InMemoryToolRegistry();
});

// Add agent loop
builder.Services.AddSingleton<AgentLoop>();

// Add memory store (in-memory for now)
builder.Services.AddSingleton<IMemoryStore>(sp =>
{
    return new SqliteMemoryStore(":memory:");
});

// Add channel collection (empty for now)
builder.Services.AddSingleton<IReadOnlyList<IChannel>>(sp => []);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Remove HTTPS redirection for local development
// app.UseHttpsRedirection();

// Serve Blazor WASM static files
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// Map Gateway endpoints
app.MapGatewayEndpoints();

// Map SignalR hubs
app.MapHub<AgentHub>("/hubs/agent");

// Serve index.html for all non-API/non-Hub routes (Blazor SPA fallback)
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>
/// Test LLM provider that returns mock responses.
/// </summary>
public class TestLlmProvider : ILlmProvider
{
    private readonly ILogger<TestLlmProvider> _logger;

    public TestLlmProvider(ILogger<TestLlmProvider> logger)
    {
        _logger = logger;
    }

    public string Name => "test";

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        return await Task.FromResult(true);
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
    {
        return await Task.FromResult<IReadOnlyList<string>>(["test-model"]);
    }

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Test provider received request with {MessageCount} messages", request.Messages.Count);
        
        var lastMessage = request.Messages.LastOrDefault();
        var response = lastMessage?.Content ?? "";
        
        return Task.FromResult(new LlmResponse(
            Content: $"Echo: {response}",
            ToolCalls: [],
            FinishReason: "stop",
            Usage: null
        ));
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var lastMessage = request.Messages.LastOrDefault();
        var response = lastMessage?.Content ?? "";
        
        yield return new LlmStreamChunk(
            ContentDelta: $"Echo: {response}",
            ToolCallDelta: null,
            FinishReason: "stop",
            Usage: null
        );
    }
}

/// <summary>
/// In-memory tool registry for testing.
/// </summary>
public class InMemoryToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();

    public void Register(ITool tool)
    {
        _tools[tool.Name] = tool;
    }

    public ITool? Get(string name)
    {
        return _tools.GetValueOrDefault(name);
    }

    public IReadOnlyList<ITool> GetAll()
    {
        return _tools.Values.ToList();
    }

    public IReadOnlyList<ClawSharp.Core.Tools.ToolSpec> GetSpecifications()
    {
        return [];
    }
}
