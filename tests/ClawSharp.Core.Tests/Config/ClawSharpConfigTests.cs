namespace ClawSharp.Core.Tests.Config;

using ClawSharp.Core.Config;
using FluentAssertions;

public class ClawSharpConfigTests
{
    [Fact]
    public void DefaultConfig_HasExpectedDefaults()
    {
        var config = new ClawSharpConfig();

        config.DefaultTemperature.Should().Be(0.7);
        config.MaxContextTokens.Should().Be(128_000);
        config.DefaultProvider.Should().BeNull();
        config.DefaultModel.Should().BeNull();
    }

    [Fact]
    public void DefaultConfig_WorkspaceDir_EndsWithClawSharpWorkspace()
    {
        var config = new ClawSharpConfig();
        config.WorkspaceDir.Should().Contain(".clawsharp");
        config.WorkspaceDir.Should().EndWith("workspace");
    }

    [Fact]
    public void DefaultConfig_DataDir_EndsWithClawSharp()
    {
        var config = new ClawSharpConfig();
        config.DataDir.Should().Contain(".clawsharp");
    }

    [Fact]
    public void DefaultConfig_SubConfigs_AreNotNull()
    {
        var config = new ClawSharpConfig();

        config.Providers.Should().NotBeNull();
        config.Memory.Should().NotBeNull();
        config.Gateway.Should().NotBeNull();
        config.Channels.Should().NotBeNull();
        config.Security.Should().NotBeNull();
        config.Heartbeat.Should().NotBeNull();
        config.Tunnel.Should().NotBeNull();
    }

    [Fact]
    public void GatewayConfig_HasCorrectDefaults()
    {
        var config = new GatewayConfig();

        config.Host.Should().Be("127.0.0.1");
        config.Port.Should().Be(8080);
        config.EnableUi.Should().BeTrue();
        config.ApiKey.Should().BeNull();
    }

    [Fact]
    public void SecurityConfig_HasCorrectDefaults()
    {
        var config = new SecurityConfig();

        config.SandboxEnabled.Should().BeTrue();
        config.AllowedCommands.Should().NotBeEmpty();
        config.AllowedCommands.Should().Contain("ls");
    }

    [Fact]
    public void HeartbeatConfig_HasCorrectDefaults()
    {
        var config = new HeartbeatConfig();

        config.Enabled.Should().BeTrue();
        config.IntervalSeconds.Should().Be(1800);
        config.Prompt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void MemoryConfig_HasCorrectDefaults()
    {
        var config = new MemoryConfig();

        config.DbPath.Should().Be("memory.db");
        config.EnableVectorSearch.Should().BeTrue();
    }

    [Fact]
    public void ProvidersConfig_HasNullProvidersByDefault()
    {
        var config = new ProvidersConfig();

        config.Openai.Should().BeNull();
        config.Anthropic.Should().BeNull();
        config.OpenRouter.Should().BeNull();
        config.Ollama.Should().BeNull();
        config.Compatible.Should().BeEmpty();
    }
}
