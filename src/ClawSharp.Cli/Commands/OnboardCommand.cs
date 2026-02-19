using System.CommandLine;

namespace ClawSharp.Cli.Commands;

/// <summary>
/// Interactive onboarding wizard for first-time ClawSharp setup.
/// </summary>
public class OnboardCommand : Command
{
    public OnboardCommand() : base("onboard", "Run the first-time setup wizard")
    {
        var nonInteractive = new Option<bool>("--non-interactive");
        nonInteractive.Description = "Run without prompts (use defaults and flags)";

        var configPath = new Option<string>("--config-path");
        configPath.Description = "Path to write config.toml";

        var workspace = new Option<string>("--workspace");
        workspace.Description = "Path to workspace directory";

        var provider = new Option<string>("--provider");
        provider.Description = "LLM provider name (e.g., openai, anthropic, ollama)";

        var apiKey = new Option<string>("--api-key");
        apiKey.Description = "API key for the provider";

        var model = new Option<string>("--model");
        model.Description = "Default model name";

        Options.Add(nonInteractive);
        Options.Add(configPath);
        Options.Add(workspace);
        Options.Add(provider);
        Options.Add(apiKey);
        Options.Add(model);

        SetAction(ctx =>
        {
            var isNonInteractive = ctx.GetValue(nonInteractive);
            var cfgPath = ctx.GetValue(configPath);
            var wsDir = ctx.GetValue(workspace);
            var prov = ctx.GetValue(provider);
            var key = ctx.GetValue(apiKey);
            var mdl = ctx.GetValue(model);

            return RunWizard(isNonInteractive, cfgPath, wsDir, prov, key, mdl);
        });
    }

    private static int RunWizard(
        bool nonInteractive,
        string? configPath,
        string? workspaceDir,
        string? provider,
        string? apiKey,
        string? model)
    {
        Console.WriteLine();
        Console.WriteLine("  🐾 Welcome to ClawSharp Setup!");
        Console.WriteLine("  ================================");
        Console.WriteLine();

        // Determine paths
        var defaultDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clawsharp");
        configPath ??= Path.Combine(defaultDataDir, "config.toml");
        workspaceDir ??= Path.Combine(defaultDataDir, "workspace");

        // Check for existing config
        if (File.Exists(configPath))
        {
            Console.WriteLine($"  ⚠️  Config file already exists at: {configPath}");
            if (!nonInteractive)
            {
                Console.Write("  Overwrite? (y/N): ");
                var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (answer != "y" && answer != "yes")
                {
                    Console.WriteLine("  Cancelled.");
                    return 0;
                }
            }
            else
            {
                Console.WriteLine("  Keeping existing config (non-interactive mode).");
                // Still create directories
                EnsureDirectories(workspaceDir);
                return 0;
            }
        }

        if (!nonInteractive)
        {
            // Interactive mode
            provider ??= PromptWithDefault("LLM Provider", "openai");
            apiKey ??= PromptOptional("API Key (optional, press Enter to skip)");
            model ??= PromptWithDefault("Default model", "gpt-4o");
            workspaceDir = PromptWithDefault("Workspace directory", workspaceDir);
        }
        else
        {
            provider ??= "openai";
            model ??= "gpt-4o";
        }

        // Create directories
        EnsureDirectories(workspaceDir);
        var configDir = Path.GetDirectoryName(configPath);
        if (configDir != null)
            Directory.CreateDirectory(configDir);

        // Generate config
        var config = GenerateConfig(provider, apiKey, model, workspaceDir);
        File.WriteAllText(configPath, config);

        Console.WriteLine();
        Console.WriteLine($"  ✅ Config written to: {configPath}");
        Console.WriteLine($"  ✅ Workspace created: {workspaceDir}");
        Console.WriteLine();
        Console.WriteLine("  Next steps:");
        Console.WriteLine("    1. Edit config.toml to fine-tune settings");
        Console.WriteLine("    2. Run 'clawsharp doctor' to verify setup");
        Console.WriteLine("    3. Run 'clawsharp agent' to start chatting");
        Console.WriteLine();

        return 0;
    }

    private static void EnsureDirectories(string workspaceDir)
    {
        Directory.CreateDirectory(workspaceDir);
        Directory.CreateDirectory(Path.Combine(workspaceDir, "memory"));
        Directory.CreateDirectory(Path.Combine(workspaceDir, "skills"));
    }

    internal static string GenerateConfig(string provider, string? apiKey, string? model, string workspaceDir)
    {
        var keyLine = !string.IsNullOrEmpty(apiKey) ? $"\napi_key = \"{apiKey}\"" : "";

        return $"""
            # ClawSharp Configuration
            # Generated by 'clawsharp onboard'

            workspace_dir = "{workspaceDir.Replace("\\", "/")}"
            default_provider = "{provider}"
            default_model = "{model ?? "gpt-4o"}"
            default_temperature = 0.7
            max_context_tokens = 128000

            [providers.{provider}]
            type = "{provider}"{keyLine}

            [memory]
            backend = "sqlite"

            [gateway]
            port = 5100
            cors_origins = ["http://localhost:5200"]

            [heartbeat]
            enabled = true
            interval_seconds = 1800

            [security]
            sandbox_enabled = false
            """;
    }

    private static string PromptWithDefault(string prompt, string defaultValue)
    {
        Console.Write($"  {prompt} [{defaultValue}]: ");
        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? defaultValue : input;
    }

    private static string? PromptOptional(string prompt)
    {
        Console.Write($"  {prompt}: ");
        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? null : input;
    }
}
