using ClawSharp.Core.Channels;
using ClawSharp.Core.Config;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ClawSharp.Channels;

/// <summary>
/// Telegram channel implementation using Telegram Bot API.
/// </summary>
public class TelegramChannel : IChannel
{
    private readonly ClawSharpConfig _config;
    private readonly ILogger<TelegramChannel> _logger;
    private ITelegramBotClient? _botClient;
    private CancellationTokenSource? _pollingCts;
    private readonly HashSet<string> _allowedUsers;

    public string Name => "telegram";

    public event Func<ChannelMessage, Task>? OnMessage;

    public TelegramChannel(ClawSharpConfig config, ILogger<TelegramChannel> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;

        var telegramConfig = config.Channels.Telegram 
            ?? throw new InvalidOperationException("Telegram configuration is required. Set Channels.Telegram in config.");
        
        if (string.IsNullOrWhiteSpace(telegramConfig.BotToken))
            throw new InvalidOperationException("Telegram BotToken is required. Set Channels.Telegram.BotToken in config.");

        _allowedUsers = telegramConfig.AllowedUsers?.ToHashSet() ?? [];
    }

    /// <summary>
    /// Internal constructor for testing with a mock bot client.
    /// </summary>
    internal TelegramChannel(ClawSharpConfig config, ILogger<TelegramChannel> logger, ITelegramBotClient botClient)
        : this(config, logger)
    {
        _botClient = botClient;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (_botClient == null)
        {
            var token = _config.Channels.Telegram?.BotToken 
                ?? throw new InvalidOperationException("BotToken not configured");
            _botClient = new TelegramBotClient(token);
        }

        _pollingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        
        _logger.LogInformation("Starting Telegram bot polling");
        
        // Start polling for updates
        _ = Task.Run(async () => await PollForUpdatesAsync(_pollingCts.Token), _pollingCts.Token);
        
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("Stopping Telegram bot");
        
        _pollingCts?.Cancel();
        
        if (_botClient != null)
        {
            await _botClient.Close();
        }
        
        _pollingCts?.Dispose();
        _pollingCts = null;
    }

    public async Task SendAsync(OutboundMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        
        if (_botClient == null)
            throw new InvalidOperationException("Bot client not initialized. Call StartAsync first.");

        // Handle file attachments (simplified - just send content without file for now)
        if (!string.IsNullOrEmpty(message.FilePath))
        {
            _logger.LogDebug("File attachment ignored for now: {FilePath}", message.FilePath);
            // TODO: Implement file sending with Telegram.Bot v22 API
        }

        // Handle long messages by splitting
        if (message.Content.Length > 4096)
        {
            await SendLongMessageAsync(message, ct);
            return;
        }

        // Send regular message using v22 API (SendMessage, not SendTextMessageAsync)
        await _botClient.SendMessage(
            chatId: message.ChatId,
            text: message.Content,
            parseMode: ParseMode.Markdown,
            disableNotification: message.Silent,
            cancellationToken: ct);
    }

    private async Task SendLongMessageAsync(OutboundMessage message, CancellationToken ct)
    {
        const int maxLength = 4096;
        var chunks = SplitMessage(message.Content, maxLength);

        foreach (var chunk in chunks)
        {
            await _botClient!.SendMessage(
                chatId: message.ChatId,
                text: chunk,
                parseMode: ParseMode.Markdown,
                disableNotification: message.Silent,
                cancellationToken: ct);
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

    private async Task PollForUpdatesAsync(CancellationToken ct)
    {
        var offset = 0;
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var updates = await _botClient!.GetUpdates(
                    offset,
                    limit: 100,
                    timeout: 30,
                    cancellationToken: ct);

                foreach (var update in updates)
                {
                    await HandleUpdateAsync(update);
                    offset = update.Id + 1;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling for Telegram updates");
                await Task.Delay(1000, ct);
            }
        }
    }

    private Task HandleUpdateAsync(Update update)
    {
        if (update.Message == null)
            return Task.CompletedTask;

        var message = update.Message;
        
        // Check if user is allowed
        if (_allowedUsers.Count > 0 && !_allowedUsers.Contains(message.From?.Id.ToString() ?? ""))
        {
            _logger.LogDebug("Ignoring message from unauthorized user: {UserId}", message.From?.Id);
            return Task.CompletedTask;
        }

        var channelMessage = new ChannelMessage(
            Id: message.MessageId.ToString(),
            Sender: message.From?.Id.ToString() ?? "unknown",
            Content: message.Text ?? "",
            Channel: Name,
            ChatId: message.Chat.Id.ToString(),
            Timestamp: message.Date,
            Media: message.Photo?.Length > 0 
                ? message.Photo.Select(p => p.FileId).ToList() 
                : null);

        _logger.LogDebug("Received message from {User}: {Content}", channelMessage.Sender, channelMessage.Content);

        return OnMessage?.Invoke(channelMessage) ?? Task.CompletedTask;
    }

    /// <summary>
    /// Simulates receiving a message for testing purposes.
    /// </summary>
    public Task SimulateMessageAsync(string userId, string content, string chatId)
    {
        // Check if user is allowed
        if (_allowedUsers.Count > 0 && !_allowedUsers.Contains(userId))
        {
            _logger.LogDebug("Ignoring message from unauthorized user: {UserId}", userId);
            return Task.CompletedTask;
        }

        var channelMessage = new ChannelMessage(
            Id: Guid.NewGuid().ToString(),
            Sender: userId,
            Content: content,
            Channel: Name,
            ChatId: chatId,
            Timestamp: DateTimeOffset.UtcNow);

        return OnMessage?.Invoke(channelMessage) ?? Task.CompletedTask;
    }
}
