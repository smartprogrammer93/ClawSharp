using ClawSharp.Core.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClawSharp.Channels;

/// <summary>
/// Manages the lifecycle of communication channels and routes messages to the message bus.
/// </summary>
public sealed class ChannelManager : IHostedService
{
    private readonly IReadOnlyList<IChannel> _channels;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<ChannelManager> _logger;
    private readonly List<ChannelStatus> _statuses = new();
    private readonly object _lock = new();
    private bool _isRunning;

    public IReadOnlyList<ChannelStatus> Statuses
    {
        get
        {
            lock (_lock)
            {
                return _statuses.ToList();
            }
        }
    }

    public ChannelManager(
        IEnumerable<IChannel> channels,
        IMessageBus messageBus,
        ILogger<ChannelManager> logger)
    {
        _channels = channels?.ToList() ?? throw new ArgumentNullException(nameof(channels));
        _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize statuses for each channel
        foreach (var channel in _channels)
        {
            _statuses.Add(new ChannelStatus
            {
                Name = channel.Name,
                IsRunning = false
            });
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting ChannelManager with {ChannelCount} channels", _channels.Count);
        _isRunning = true;

        foreach (var channel in _channels)
        {
            var status = GetStatus(channel.Name);
            try
            {
                _logger.LogInformation("Starting channel {ChannelName}", channel.Name);
                
                // Subscribe to incoming messages
                channel.OnMessage += HandleChannelMessage;
                
                await channel.StartAsync(cancellationToken);
                
                status.IsRunning = true;
                status.LastStarted = DateTimeOffset.UtcNow;
                status.ErrorMessage = null;
                
                _logger.LogInformation("Channel {ChannelName} started successfully", channel.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start channel {ChannelName}", channel.Name);
                status.IsRunning = false;
                status.ErrorMessage = ex.Message;
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping ChannelManager");
        _isRunning = false;

        foreach (var channel in _channels)
        {
            var status = GetStatus(channel.Name);
            try
            {
                _logger.LogInformation("Stopping channel {ChannelName}", channel.Name);
                
                await channel.StopAsync(cancellationToken);
                
                // Unsubscribe from messages
                channel.OnMessage -= HandleChannelMessage;
                
                status.IsRunning = false;
                
                _logger.LogInformation("Channel {ChannelName} stopped successfully", channel.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping channel {ChannelName}", channel.Name);
                status.ErrorMessage = ex.Message;
            }
        }
    }

    private Task HandleChannelMessage(ChannelMessage message)
    {
        if (!_isRunning)
        {
            _logger.LogWarning("Received message but ChannelManager is not running. Message: {MessageId}", message.Id);
            return Task.CompletedTask;
        }

        _logger.LogDebug("Received message {MessageId} from channel {ChannelName}", message.Id, message.Channel);
        
        // Publish to message bus for the agent loop to consume
        return _messageBus.PublishAsync(message);
    }

    private ChannelStatus GetStatus(string channelName)
    {
        lock (_lock)
        {
            return _statuses.First(s => s.Name == channelName);
        }
    }
}
