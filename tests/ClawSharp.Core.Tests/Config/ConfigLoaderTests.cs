namespace ClawSharp.Core.Tests.Config;

using ClawSharp.Core.Config;
using ClawSharp.Infrastructure.Config;
using FluentAssertions;

public class ConfigLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _envVarsToClean = [];

    public ConfigLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"clawsharp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
        foreach (var key in _envVarsToClean)
            Environment.SetEnvironmentVariable(key, null);
    }

    private void SetEnv(string key, string value)
    {
        _envVarsToClean.Add(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    private string WriteTempToml(string content)
    {
        var path = Path.Combine(_tempDir, "config.toml");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaultConfig()
    {
        var config = TomlConfigLoader.Load(Path.Combine(_tempDir, "nonexistent.toml"));

        config.Should().NotBeNull();
        config.DefaultTemperature.Should().Be(0.7);
        config.MaxContextTokens.Should().Be(128_000);
    }

    [Fact]
    public void Load_ValidToml_ParsesCorrectly()
    {
        var toml = """
            default_provider = "openai"
            default_model = "gpt-4o"
            default_temperature = 0.5
            max_context_tokens = 64000

            [gateway]
            host = "0.0.0.0"
            port = 9090
            enable_ui = false
            """;
        var path = WriteTempToml(toml);

        var config = TomlConfigLoader.Load(path);

        config.DefaultProvider.Should().Be("openai");
        config.DefaultModel.Should().Be("gpt-4o");
        config.DefaultTemperature.Should().Be(0.5);
        config.MaxContextTokens.Should().Be(64000);
        config.Gateway.Host.Should().Be("0.0.0.0");
        config.Gateway.Port.Should().Be(9090);
        config.Gateway.EnableUi.Should().BeFalse();
    }

    [Fact]
    public void Load_TomlWithProviders_ParsesProviderEntries()
    {
        var toml = """
            [providers.openai]
            api_key = "sk-test-123"
            base_url = "https://api.openai.com/v1"
            default_model = "gpt-4o"

            [providers.anthropic]
            api_key = "ant-test-456"
            default_model = "claude-sonnet-4-20250514"
            """;
        var path = WriteTempToml(toml);

        var config = TomlConfigLoader.Load(path);

        config.Providers.Openai.Should().NotBeNull();
        config.Providers.Openai!.ApiKey.Should().Be("sk-test-123");
        config.Providers.Openai.BaseUrl.Should().Be("https://api.openai.com/v1");
        config.Providers.Anthropic.Should().NotBeNull();
        config.Providers.Anthropic!.ApiKey.Should().Be("ant-test-456");
    }

    [Fact]
    public void Load_EnvironmentVariablesOverrideToml()
    {
        var toml = """
            default_provider = "openai"
            """;
        var path = WriteTempToml(toml);

        SetEnv("CLAWSHARP_DEFAULT_PROVIDER", "anthropic");
        SetEnv("CLAWSHARP_DEFAULT_MODEL", "claude-opus");

        var config = TomlConfigLoader.Load(path);

        config.DefaultProvider.Should().Be("anthropic");
        config.DefaultModel.Should().Be("claude-opus");
    }

    [Fact]
    public void Load_ApiKeyEnvVarsOverrideConfig()
    {
        var toml = """
            [providers.openai]
            api_key = "from-toml"
            """;
        var path = WriteTempToml(toml);

        SetEnv("OPENAI_API_KEY", "from-env");
        SetEnv("ANTHROPIC_API_KEY", "ant-from-env");

        var config = TomlConfigLoader.Load(path);

        config.Providers.Openai!.ApiKey.Should().Be("from-env");
        config.Providers.Anthropic.Should().NotBeNull();
        config.Providers.Anthropic!.ApiKey.Should().Be("ant-from-env");
    }

    [Fact]
    public void Load_InvalidToml_ThrowsMeaningfulError()
    {
        var path = WriteTempToml("this is not [valid toml");

        var act = () => TomlConfigLoader.Load(path);

        act.Should().Throw<Exception>().Which.Message.Should().Contain("config");
    }

    [Fact]
    public void Load_ChannelsConfig_ParsesCorrectly()
    {
        var toml = """
            [channels.telegram]
            bot_token = "123:ABC"
            allowed_users = ["user1", "user2"]
            use_webhook = true
            """;
        var path = WriteTempToml(toml);

        var config = TomlConfigLoader.Load(path);

        config.Channels.Telegram.Should().NotBeNull();
        config.Channels.Telegram!.BotToken.Should().Be("123:ABC");
        config.Channels.Telegram.AllowedUsers.Should().HaveCount(2);
        config.Channels.Telegram.UseWebhook.Should().BeTrue();
    }

    [Fact]
    public void Load_SecurityConfig_ParsesCorrectly()
    {
        var toml = """
            [security]
            sandbox_enabled = false
            allowed_commands = ["git", "docker"]
            pairing_secret = "my-secret"
            """;
        var path = WriteTempToml(toml);

        var config = TomlConfigLoader.Load(path);

        config.Security.SandboxEnabled.Should().BeFalse();
        config.Security.AllowedCommands.Should().Contain("git");
        config.Security.PairingSecret.Should().Be("my-secret");
    }

    [Fact]
    public void Load_TunnelConfig_ParsesCorrectly()
    {
        var toml = """
            [tunnel]
            provider = "cloudflare"
            token = "cf-token"
            domain = "my.domain.com"
            """;
        var path = WriteTempToml(toml);

        var config = TomlConfigLoader.Load(path);

        config.Tunnel.Provider.Should().Be("cloudflare");
        config.Tunnel.Token.Should().Be("cf-token");
        config.Tunnel.Domain.Should().Be("my.domain.com");
    }

    [Fact]
    public void Load_HeartbeatConfig_ParsesCorrectly()
    {
        var toml = """
            [heartbeat]
            enabled = false
            interval_seconds = 600
            prompt = "Custom prompt"
            """;
        var path = WriteTempToml(toml);

        var config = TomlConfigLoader.Load(path);

        config.Heartbeat.Enabled.Should().BeFalse();
        config.Heartbeat.IntervalSeconds.Should().Be(600);
        config.Heartbeat.Prompt.Should().Be("Custom prompt");
    }

    [Fact]
    public void Load_ConfigPathFromEnvVar_IsUsed()
    {
        var toml = """
            default_provider = "from-env-path"
            """;
        var path = WriteTempToml(toml);

        SetEnv("CLAWSHARP_CONFIG_PATH", path);

        var config = TomlConfigLoader.Load();

        config.DefaultProvider.Should().Be("from-env-path");
    }
}
