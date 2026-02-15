using ClawSharp.Core.Config;

namespace ClawSharp.TestHelpers;

/// <summary>
/// Factory methods for creating test configurations and common test objects.
/// </summary>
public static class TestHelpers
{
    /// <summary>Creates a minimal ClawSharpConfig suitable for unit tests.</summary>
    public static ClawSharpConfig CreateTestConfig(Action<ClawSharpConfig>? configure = null)
    {
        var config = new ClawSharpConfig
        {
            WorkspaceDir = Path.Combine(Path.GetTempPath(), "clawsharp-test", Guid.NewGuid().ToString("N")),
            DataDir = Path.Combine(Path.GetTempPath(), "clawsharp-test-data", Guid.NewGuid().ToString("N")),
            DefaultProvider = "fake",
            DefaultModel = "fake-model",
            DefaultTemperature = 0.0,
            MaxContextTokens = 4096,
            Providers = new ProvidersConfig
            {
                Openai = new ProviderEntry { ApiKey = "sk-test-key", DefaultModel = "gpt-4o" },
                Anthropic = new ProviderEntry { ApiKey = "sk-ant-test", DefaultModel = "claude-sonnet-4-20250514" },
            },
            Gateway = new GatewayConfig { Host = "127.0.0.1", Port = 0, EnableUi = false },
            Security = new SecurityConfig { SandboxEnabled = false },
        };

        configure?.Invoke(config);
        return config;
    }

    /// <summary>Creates a temporary directory that is cleaned up on dispose.</summary>
    public static TempDirectory CreateTempDirectory() => new();
}

/// <summary>A temporary directory that deletes itself on dispose.</summary>
public sealed class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "clawsharp-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
    }
}
