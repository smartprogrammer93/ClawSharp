namespace ClawSharp.Core.Config;

/// <summary>Channel configurations for messaging platforms.</summary>
public class ChannelsConfig
{
    public TelegramChannelConfig? Telegram { get; set; }
    public DiscordChannelConfig? Discord { get; set; }
    public SlackChannelConfig? Slack { get; set; }
}

public class TelegramChannelConfig
{
    public string? BotToken { get; set; }
    public List<string> AllowedUsers { get; set; } = [];
    public bool UseWebhook { get; set; }
}

public class DiscordChannelConfig
{
    public string? BotToken { get; set; }
    public List<string> AllowedGuilds { get; set; } = [];
}

public class SlackChannelConfig
{
    public string? BotToken { get; set; }
    public string? AppToken { get; set; }
    public string? SigningSecret { get; set; }
    public List<string> AllowedChannels { get; set; } = [];
}
