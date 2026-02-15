using ClawSharp.Core.Channels;
using ClawSharp.Core.Config;
using Microsoft.Extensions.Logging;
using Discord;
using Discord.WebSocket;
using System.Text.RegularExpressions;

namespace ClawSharp.Channels;

/// <summary>
/// Discord channel implementation using Discord.Net.
/// </summary>
public class DiscordChannel : ClawSharp.Core.Channels.IChannel
{
    private readonly ClawSharpConfig _config;
    private readonly ILogger<DiscordChannel> _logger;
    private DiscordSocketClient? _client;
    private readonly HashSet<string> _allowedGuilds;

    public string Name => "discord";

    public event Func<ChannelMessage, Task>? OnMessage;

    public DiscordChannel(ClawSharpConfig config, ILogger<DiscordChannel> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;

        var discordConfig = config.Channels.Discord 
            ?? throw new InvalidOperationException("Discord configuration is required. Set Channels.Discord in config.");
        
        if (string.IsNullOrWhiteSpace(discordConfig.BotToken))
            throw new InvalidOperationException("Discord BotToken is required. Set Channels.Discord.BotToken in config.");

        _allowedGuilds = discordConfig.AllowedGuilds?.ToHashSet() ?? [];
    }

    /// <summary>
    /// Internal constructor for testing.
    /// </summary>
    internal DiscordChannel(ClawSharpConfig config, ILogger<DiscordChannel> logger, DiscordSocketClient client)
        : this(config, logger)
    {
        _client = client;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (_client == null)
        {
            var token = _config.Channels.Discord?.BotToken 
                ?? throw new InvalidOperationException("BotToken not configured");
            
            var discordConfig = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.MessageContent | GatewayIntents.DirectMessages | GatewayIntents.Guilds
            };
            
            _client = new DiscordSocketClient(discordConfig);
            
            _client.MessageReceived += HandleSocketMessageAsync;
            
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
        }

        _logger.LogInformation("Discord bot started");
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("Stopping Discord bot");
        
        if (_client != null)
        {
            await _client.LogoutAsync();
            await _client.StopAsync();
            _client.Dispose();
            _client = null;
        }
    }

    public async Task SendAsync(OutboundMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        
        if (_client == null)
            throw new InvalidOperationException("Discord client not initialized. Call StartAsync first.");

        // Handle long messages by splitting at 2000 chars (Discord's limit)
        if (message.Content.Length > 2000)
        {
            await SendLongMessageAsync(message, ct);
            return;
        }

        // Send to the channel/user
        if (ulong.TryParse(message.ChatId, out var channelId))
        {
            if (_client.GetChannel(channelId) is IMessageChannel channel)
            {
                await channel.SendMessageAsync(message.Content, message.Silent);
            }
        }
    }

    private async Task SendLongMessageAsync(OutboundMessage message, CancellationToken ct)
    {
        const int maxLength = 2000;
        var chunks = SplitMessage(message.Content, maxLength);

        if (ulong.TryParse(message.ChatId, out var channelId))
        {
            if (_client?.GetChannel(channelId) is IMessageChannel channel)
            {
                foreach (var chunk in chunks)
                {
                    await channel.SendMessageAsync(chunk, message.Silent);
                }
            }
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

    private Task HandleSocketMessageAsync(SocketMessage msg)
    {
        // Ignore bot messages
        if (msg.Author.IsBot)
            return Task.CompletedTask;

        // Check if this is a guild message (need to be mentioned) or DM
        var isDm = msg.Channel is IDMChannel;
        
        if (msg.Channel is IGuildChannel guildChannelItem)
        {
            var guild = guildChannelItem.Guild;
            
            // Check allowed guilds
            if (_allowedGuilds.Count > 0 && guild != null && !_allowedGuilds.Contains(guild.Id.ToString()))
            {
                _logger.LogDebug("Ignoring message from disallowed guild: {GuildId}", guild.Id);
                return Task.CompletedTask;
            }
            
            // Check if bot is mentioned
            var content = msg.Content;
            var botMentions = msg.MentionedUsers.Any(u => u.IsBot);
            
            if (!botMentions)
            {
                // Not mentioned, ignore unless it's a DM
                return Task.CompletedTask;
            }
            
            // Strip mention from content
            content = Regex.Replace(content, @"<@!?\d+>", "").Trim();
            
            var channelMessage = new ChannelMessage(
                Id: msg.Id.ToString(),
                Sender: msg.Author.Id.ToString(),
                Content: content,
                Channel: Name,
                ChatId: msg.Channel.Id.ToString(),
                Timestamp: msg.Timestamp);

            return OnMessage?.Invoke(channelMessage) ?? Task.CompletedTask;
        }
        
        // Handle DM
        if (isDm)
        {
            var channelMessage = new ChannelMessage(
                Id: msg.Id.ToString(),
                Sender: msg.Author.Id.ToString(),
                Content: msg.Content,
                Channel: Name,
                ChatId: msg.Channel.Id.ToString(),
                Timestamp: msg.Timestamp);

            return OnMessage?.Invoke(channelMessage) ?? Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Simulates receiving a message for testing purposes.
    /// </summary>
    public Task SimulateMessageAsync(string userId, string content, string channelId, 
        bool isBot = false, bool isDm = false, string? guildId = null)
    {
        // Check allowed guilds
        if (!isDm && guildId != null && _allowedGuilds.Count > 0 && !_allowedGuilds.Contains(guildId))
        {
            _logger.LogDebug("Ignoring message from disallowed guild: {GuildId}", guildId);
            return Task.CompletedTask;
        }

        // Ignore bot messages
        if (isBot)
        {
            return Task.CompletedTask;
        }

        // Strip mention from content - match Discord format <@userid> or <@!userid>
        var processedContent = Regex.Replace(content, @"<@!?[a-zA-Z0-9]+>", "").Trim();

        var channelMessage = new ChannelMessage(
            Id: Guid.NewGuid().ToString(),
            Sender: userId,
            Content: processedContent,
            Channel: Name,
            ChatId: channelId,
            Timestamp: DateTimeOffset.UtcNow);

        return OnMessage?.Invoke(channelMessage) ?? Task.CompletedTask;
    }
}
