using Microsoft.AspNetCore.SignalR.Client;

namespace ClawSharp.UI.Services;

/// <summary>
/// Service for managing SignalR connection to the agent hub.
/// </summary>
public class SignalRService : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly string _hubUrl;
    private readonly List<Func<string, Task>> _messageHandlers = new();
    private readonly List<Func<string, Task>> _toolExecutionHandlers = new();
    private readonly List<Func<Task>> _connectionHandlers = new();

    public SignalRService()
    {
        // Get the base URL from the current location
        var baseUrl = GetBaseUrl();
        _hubUrl = $"{baseUrl}hubs/agent";
    }

    private static string GetBaseUrl()
    {
        // Try to get from window.location
        if (OperatingSystem.IsBrowser())
        {
            return "";
        }
        // Default to localhost for development
        return "http://localhost:8080/";
    }

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_connection != null)
        {
            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();

        // Register event handlers
        _connection.On<string>("OnMessage", async (message) =>
        {
            foreach (var handler in _messageHandlers)
            {
                await handler(message);
            }
        });

        _connection.On<string>("OnToolExecution", async (toolJson) =>
        {
            foreach (var handler in _toolExecutionHandlers)
            {
                await handler(toolJson);
            }
        });

        _connection.On("OnComplete", async () =>
        {
            foreach (var handler in _connectionHandlers)
            {
                await handler();
            }
        });

        try
        {
            await _connection.StartAsync(ct);
        }
        catch (Exception)
        {
            // Connection failed, will retry automatically
            _connection = null;
        }
    }

    public void OnMessage(Func<string, Task> handler)
    {
        _messageHandlers.Add(handler);
    }

    public void OnToolExecution(Func<string, Task> handler)
    {
        _toolExecutionHandlers.Add(handler);
    }

    public void OnComplete(Func<Task> handler)
    {
        _connectionHandlers.Add(handler);
    }

    public async Task SendMessageAsync(string message, string? sessionKey = null)
    {
        if (_connection == null)
        {
            await ConnectAsync();
        }

        if (_connection != null)
        {
            await _connection.InvokeAsync("SendMessage", message, sessionKey);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }
}
