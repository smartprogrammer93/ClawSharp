using ClawSharp.Core.Config;
using ClawSharp.Core.Providers;
using ClawSharp.Core.Tools;
using ClawSharp.Infrastructure.Heartbeat;
using ClawSharp.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ToolSpec = ClawSharp.Core.Tools.ToolSpec;
using TestHelperFactory = ClawSharp.TestHelpers.TestHelpers;

namespace ClawSharp.Infrastructure.Tests.Heartbeat;

public class HeartbeatServiceTests : IDisposable
{
    private readonly TempDirectory _tempDir;
    private readonly ClawSharpConfig _config;

    public HeartbeatServiceTests()
    {
        _tempDir = TestHelperFactory.CreateTempDirectory();
        _config = TestHelperFactory.CreateTestConfig(c =>
        {
            c.WorkspaceDir = _tempDir.Path;
            c.Heartbeat.Enabled = true;
            c.Heartbeat.IntervalSeconds = 1;
        });
    }

    public void Dispose()
    {
        _tempDir.Dispose();
    }

    private static ClawSharpConfig CreateTestConfig(bool enabled = true, int intervalSeconds = 1)
    {
        return new ClawSharpConfig
        {
            WorkspaceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
            Heartbeat = new HeartbeatConfig
            {
                Enabled = enabled,
                IntervalSeconds = intervalSeconds,
                Prompt = "Test heartbeat prompt. If nothing needs attention, reply HEARTBEAT_OK."
            }
        };
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        var provider = Substitute.For<ILlmProvider>();
        var tools = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<HeartbeatService>>();

        var act = () => new HeartbeatService(null!, provider, tools, logger);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullProvider_ThrowsArgumentNullException()
    {
        var config = CreateTestConfig();
        var tools = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<HeartbeatService>>();

        var act = () => new HeartbeatService(config, null!, tools, logger);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsEnabled_WhenConfigDisabled_ReturnsFalse()
    {
        var config = CreateTestConfig(enabled: false);
        var provider = Substitute.For<ILlmProvider>();
        var tools = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);

        service.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_WhenConfigEnabled_ReturnsTrue()
    {
        var config = CreateTestConfig(enabled: true);
        var provider = Substitute.For<ILlmProvider>();
        var tools = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);

        service.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Interval_ReturnsConfiguredInterval()
    {
        var config = CreateTestConfig(intervalSeconds: 300);
        var provider = Substitute.For<ILlmProvider>();
        var tools = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);

        service.Interval.Should().Be(TimeSpan.FromSeconds(300));
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_WhenDisabled_ReturnsSkippedResult()
    {
        var config = CreateTestConfig(enabled: false);
        var provider = Substitute.For<ILlmProvider>();
        var tools = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);

        var result = await service.ExecuteHeartbeatAsync();

        result.Skipped.Should().BeTrue();
        result.Response.Should().BeNull();
        await provider.DidNotReceive().CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_WhenEnabled_CallsProvider()
    {
        var config = CreateTestConfig(enabled: true);
        var provider = Substitute.For<ILlmProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("HEARTBEAT_OK", [], "stop", null));
        var tools = Substitute.For<IToolRegistry>();
        tools.GetSpecifications().Returns([]);
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);

        var result = await service.ExecuteHeartbeatAsync();

        result.Skipped.Should().BeFalse();
        await provider.Received(1).CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_HeartbeatOk_SetsNoDeliveryRequired()
    {
        var config = CreateTestConfig(enabled: true);
        var provider = Substitute.For<ILlmProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("HEARTBEAT_OK", [], "stop", null));
        var tools = Substitute.For<IToolRegistry>();
        tools.GetSpecifications().Returns([]);
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);

        var result = await service.ExecuteHeartbeatAsync();

        result.RequiresDelivery.Should().BeFalse();
        result.Response.Should().Be("HEARTBEAT_OK");
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_HeartbeatOkCaseInsensitive_SetsNoDeliveryRequired()
    {
        var config = CreateTestConfig(enabled: true);
        var provider = Substitute.For<ILlmProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("heartbeat_ok", [], "stop", null));
        var tools = Substitute.For<IToolRegistry>();
        tools.GetSpecifications().Returns([]);
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);

        var result = await service.ExecuteHeartbeatAsync();

        result.RequiresDelivery.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_HeartbeatOkContained_SetsNoDeliveryRequired()
    {
        var config = CreateTestConfig(enabled: true);
        var provider = Substitute.For<ILlmProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Checked everything, HEARTBEAT_OK", [], "stop", null));
        var tools = Substitute.For<IToolRegistry>();
        tools.GetSpecifications().Returns([]);
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);

        var result = await service.ExecuteHeartbeatAsync();

        result.RequiresDelivery.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_NonOkResponse_SetsDeliveryRequired()
    {
        var config = CreateTestConfig(enabled: true);
        var provider = Substitute.For<ILlmProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("You have 3 new emails!", [], "stop", null));
        var tools = Substitute.For<IToolRegistry>();
        tools.GetSpecifications().Returns([]);
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);

        var result = await service.ExecuteHeartbeatAsync();

        result.RequiresDelivery.Should().BeTrue();
        result.Response.Should().Be("You have 3 new emails!");
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_ReadsHeartbeatMd_WhenExists()
    {
        var workspaceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(workspaceDir);
        var heartbeatMdPath = Path.Combine(workspaceDir, "HEARTBEAT.md");
        await File.WriteAllTextAsync(heartbeatMdPath, "Check emails every hour.");

        var config = new ClawSharpConfig
        {
            WorkspaceDir = workspaceDir,
            Heartbeat = new HeartbeatConfig
            {
                Enabled = true,
                IntervalSeconds = 1,
                Prompt = "Test prompt"
            }
        };

        var provider = Substitute.For<ILlmProvider>();
        LlmRequest? capturedRequest = null;
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedRequest = call.Arg<LlmRequest>();
                return new LlmResponse("HEARTBEAT_OK", [], "stop", null);
            });
        var tools = Substitute.For<IToolRegistry>();
        tools.GetSpecifications().Returns([]);
        var logger = Substitute.For<ILogger<HeartbeatService>>();

        try
        {
            var service = new HeartbeatService(config, provider, tools, logger);

            await service.ExecuteHeartbeatAsync();

            capturedRequest.Should().NotBeNull();
            var allContent = string.Join(" ", capturedRequest!.Messages.Select(m => m.Content));
            allContent.Should().Contain("Check emails every hour.");
        }
        finally
        {
            Directory.Delete(workspaceDir, true);
        }
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_NoHeartbeatMd_StillWorks()
    {
        var workspaceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(workspaceDir);

        var config = new ClawSharpConfig
        {
            WorkspaceDir = workspaceDir,
            Heartbeat = new HeartbeatConfig
            {
                Enabled = true,
                IntervalSeconds = 1,
                Prompt = "Test prompt"
            }
        };

        var provider = Substitute.For<ILlmProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("HEARTBEAT_OK", [], "stop", null));
        var tools = Substitute.For<IToolRegistry>();
        tools.GetSpecifications().Returns([]);
        var logger = Substitute.For<ILogger<HeartbeatService>>();

        try
        {
            var service = new HeartbeatService(config, provider, tools, logger);

            var result = await service.ExecuteHeartbeatAsync();

            result.Skipped.Should().BeFalse();
            result.Response.Should().Be("HEARTBEAT_OK");
        }
        finally
        {
            Directory.Delete(workspaceDir, true);
        }
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_IncludesPromptFromConfig()
    {
        var config = CreateTestConfig(enabled: true);
        config.Heartbeat.Prompt = "Custom heartbeat prompt!";

        var provider = Substitute.For<ILlmProvider>();
        LlmRequest? capturedRequest = null;
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedRequest = call.Arg<LlmRequest>();
                return new LlmResponse("HEARTBEAT_OK", [], "stop", null);
            });
        var tools = Substitute.For<IToolRegistry>();
        tools.GetSpecifications().Returns([]);
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);

        await service.ExecuteHeartbeatAsync();

        capturedRequest.Should().NotBeNull();
        var allContent = string.Join(" ", capturedRequest!.Messages.Select(m => m.Content));
        allContent.Should().Contain("Custom heartbeat prompt!");
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_WithToolSpecs_PassesToProvider()
    {
        var config = CreateTestConfig(enabled: true);
        var provider = Substitute.For<ILlmProvider>();
        LlmRequest? capturedRequest = null;
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedRequest = call.Arg<LlmRequest>();
                return new LlmResponse("HEARTBEAT_OK", [], "stop", null);
            });

        var schema = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
            """{ "type": "object", "properties": {} }""");
        var toolSpec = new ToolSpec("weather", "Get weather info", schema);

        var tools = Substitute.For<IToolRegistry>();
        tools.GetSpecifications().Returns([toolSpec]);
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);

        await service.ExecuteHeartbeatAsync();

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Tools.Should().NotBeNull();
        capturedRequest.Tools.Should().HaveCount(1);
        capturedRequest.Tools![0].Name.Should().Be("weather");
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_NoTools_NullToolsInRequest()
    {
        var config = CreateTestConfig(enabled: true);
        var provider = Substitute.For<ILlmProvider>();
        LlmRequest? capturedRequest = null;
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedRequest = call.Arg<LlmRequest>();
                return new LlmResponse("HEARTBEAT_OK", [], "stop", null);
            });

        var tools = Substitute.For<IToolRegistry>();
        tools.GetSpecifications().Returns([]);
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);

        await service.ExecuteHeartbeatAsync();

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Tools.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_ProviderThrows_ReturnsErrorResult()
    {
        var config = CreateTestConfig(enabled: true);
        var provider = Substitute.For<ILlmProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns<LlmResponse>(x => throw new Exception("Provider error"));
        var tools = Substitute.For<IToolRegistry>();
        tools.GetSpecifications().Returns([]);
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);

        var result = await service.ExecuteHeartbeatAsync();

        result.Error.Should().NotBeNull();
        result.Error.Should().Contain("Provider error");
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_CancellationRequested_Throws()
    {
        var config = CreateTestConfig(enabled: true);
        var provider = Substitute.For<ILlmProvider>();
        var tools = Substitute.For<IToolRegistry>();
        tools.GetSpecifications().Returns([]);
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => service.ExecuteHeartbeatAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void LastExecutionTime_BeforeFirstRun_IsNull()
    {
        var config = CreateTestConfig();
        var provider = Substitute.For<ILlmProvider>();
        var tools = Substitute.For<IToolRegistry>();
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);

        service.LastExecutionTime.Should().BeNull();
    }

    [Fact]
    public async Task LastExecutionTime_AfterRun_IsUpdated()
    {
        var config = CreateTestConfig(enabled: true);
        var provider = Substitute.For<ILlmProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("HEARTBEAT_OK", [], "stop", null));
        var tools = Substitute.For<IToolRegistry>();
        tools.GetSpecifications().Returns([]);
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);

        var before = DateTimeOffset.UtcNow;

        await service.ExecuteHeartbeatAsync();

        service.LastExecutionTime.Should().NotBeNull();
        service.LastExecutionTime!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task LastExecutionTime_AfterError_IsUpdated()
    {
        var config = CreateTestConfig(enabled: true);
        var provider = Substitute.For<ILlmProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns<LlmResponse>(x => throw new Exception("Provider error"));
        var tools = Substitute.For<IToolRegistry>();
        tools.GetSpecifications().Returns([]);
        var logger = Substitute.For<ILogger<HeartbeatService>>();
        var service = new HeartbeatService(config, provider, tools, logger);

        var before = DateTimeOffset.UtcNow;

        await service.ExecuteHeartbeatAsync();

        service.LastExecutionTime.Should().NotBeNull();
        service.LastExecutionTime!.Value.Should().BeOnOrAfter(before);
    }
}
