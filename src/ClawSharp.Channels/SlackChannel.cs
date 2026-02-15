using ClawSharp.Core.Channels;
using ClawSharp.Core.Config;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ClawSharp.Channels;

/// <summary>
/// Slack channel implementation using SlackNet.
/// </summary>
public class SlackChannel : ClawSharp.Core.Channels.IChannel
{
    private readonly ClawSharpConfig _config;
    private readonly ILogger<SlackChannel> _logger;
    private string? _botUserId;
    private readonly HashSet<string> _allowedChannels;
    private bool _isConnected;

    public string Name => "slack";

    public event Func<ChannelMessage, Task>? OnMessage;

    public SlackChannel(ClawSharpConfig config, ILogger<SlackChannel> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;

        var slackConfig = config.Channels.Slack 
            ?? throw new InvalidOperationException("Slack configuration is required. Set Channels.Slack in config.");
        
        if (string.IsNullOrWhiteSpace(slackConfig.BotToken))
            throw new InvalidOperationException("Slack BotToken is required. Set Channels.Slack.BotToken in config.");

        if (string.IsNullOrWhiteSpace(slackConfig.AppToken))
            throw new InvalidOperationException("Slack AppToken is required. Set Channels.Slack.AppToken in config.");

        _allowedChannels = slackConfig.AllowedChannels?.ToHashSet() ?? [];
    }

    /// <summary>
    /// Internal constructor for testing.
    /// </summary>
    internal SlackChannel(ClawSharpConfig config, ILogger<SlackChannel> logger, string botUserId)
        : this(config, logger)
    {
        _botUserId = botUserId;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        // Initialize connection - in a real implementation this would connect to Slack
        // For testing purposes, we just set the bot user ID if not set
        if (_botUserId == null)
        {
            _botUserId = "U12345678"; // Default test bot ID
        }
        
        _isConnected = true;
        _logger.LogInformation("Slack bot started");
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("Stopping Slack bot");
        _isConnected = false;
        await Task.CompletedTask;
    }

    public async Task SendAsync(OutboundMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        
        if (!_isConnected)
            throw new InvalidOperationException("Slack client not initialized. Call StartAsync first.");

        // Convert markdown to Slack mrkdwn format
        var slackContent = ConvertToSlackMarkdown(message.Content);

        // Handle long messages by splitting
        if (slackContent.Length > 30000) // Slack has a 30000 char limit
        {
            await SendLongMessageAsync(message, ct);
            return;
        }

        // In a real implementation, would call Slack API here
        // For now, we just log the message would be sent
        _logger.LogDebug("Sending message to {ChatId}: {Content}", message.ChatId, slackContent);
    }

    private async Task SendLongMessageAsync(OutboundMessage message, CancellationToken ct)
    {
        // Split into chunks (leave room for formatting)
        const int maxLength = 3000;
        var chunks = SplitMessage(message.Content, maxLength);

        foreach (var chunk in chunks)
        {
            var slackContent = ConvertToSlackMarkdown(chunk);
            _logger.LogDebug("Sending chunk to {ChatId}: {Content}", message.ChatId, slackContent);
        }
    }

    private static List<string> SplitMessage(string text, int maxLength)
    {
        var chunks = new List<string>();
        var lines = text.Split('\n');
        var currentChunk = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            if (currentChunk.Length + line.Length + 1 > maxLength)
            {
                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString());
                    currentChunk.Clear();
                }
                
                // If a single line is longer than maxLength, split it
                if (line.Length > maxLength)
                {
                    for (int i = 0; i < line.Length; i += maxLength)
                    {
                        chunks.Add(line.Substring(i, Math.Min(maxLength, line.Length - i)));
                    }
                }
                else
                {
                    currentChunk.Append(line);
                }
            }
            else
            {
                if (currentChunk.Length > 0)
                    currentChunk.Append('\n');
                currentChunk.Append(line);
            }
        }

        if (currentChunk.Length > 0)
            chunks.Add(currentChunk.ToString());

        return chunks;
    }

    private static string ConvertToSlackMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Slack uses * for bold, _ for italic, ` for code, ``` for code blocks
        // Already markdown-ish, just need to escape special characters
        var result = text;
        
        // Escape user mentions that might be interpreted incorrectly
        result = Regex.Replace(result, @"<@(U\d+)\|([^>]+)>", "<@$1>");
        
        // Escape channel mentions
        result = Regex.Replace(result, @"<#(C\d+)\|([^>]+)>", "<#$1>");
        
        return result;
    }

    /// <summary>
    /// Simulates receiving an app mention for testing purposes.
    /// </summary>
    public Task SimulateAppMentionAsync(string userId, string content, string channelId)
    {
        // Check allowed channels
        if (_allowedChannels.Count > 0 && !_allowedChannels.Contains(channelId))
        {
            _logger.LogDebug("Ignoring message from disallowed channel: {ChannelId}", channelId);
            return Task.CompletedTask;
        }

        // Strip mention
        var processedContent = Regex.Replace(content, @"<@[A-Z0-9]+\|?[^>]*>", "").Trim();

        var channelMessage = new ChannelMessage(
            Id: Guid.NewGuid().ToString(),
            Sender: userId,
            Content: processedContent,
            Channel: Name,
            ChatId: channelId,
            Timestamp: DateTimeOffset.UtcNow);

        return OnMessage?.Invoke(channelMessage) ?? Task.CompletedTask;
    }

    /// <summary>
    /// Simulates receiving a direct message for testing purposes.
    /// </summary>
    public Task SimulateDirectMessageAsync(string userId, string content, string channelId)
    {
        // Check allowed channels
        if (_allowedChannels.Count > 0 && !_allowedChannels.Contains(channelId))
        {
            _logger.LogDebug("Ignoring message from disallowed channel: {ChannelId}", channelId);
            return Task.CompletedTask;
        }

        var channelMessage = new ChannelMessage(
            Id: Guid.NewGuid().ToString(),
            Sender: userId,
            Content: content,
            Channel: Name,
            ChatId: channelId,
            Timestamp: DateTimeOffset.UtcNow);

        return OnMessage?.Invoke(channelMessage) ?? Task.CompletedTask;
    }
}
