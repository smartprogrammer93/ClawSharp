namespace ClawSharp.Infrastructure.Config;

using ClawSharp.Core.Config;
using Tomlyn;
using Tomlyn.Model;

/// <summary>Loads ClawSharp configuration from TOML files with environment variable overrides.</summary>
public static class TomlConfigLoader
{
    /// <summary>
    /// Loads configuration from the specified path, CLAWSHARP_CONFIG_PATH env var, or the default location.
    /// Missing files return default config. Environment variables always take precedence.
    /// </summary>
    public static ClawSharpConfig Load(string? configPath = null)
    {
        configPath ??= Environment.GetEnvironmentVariable("CLAWSHARP_CONFIG_PATH")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".clawsharp", "config.toml");

        ClawSharpConfig config;

        if (!File.Exists(configPath))
        {
            config = new ClawSharpConfig();
        }
        else
        {
            var toml = File.ReadAllText(configPath);
            try
            {
                config = Toml.ToModel<ClawSharpConfig>(toml,
                    options: new TomlModelOptions
                    {
                        ConvertPropertyName = name => ToSnakeCase(name),
                    });
            }
            catch (TomlException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to parse config file '{configPath}': {ex.Message}", ex);
            }
        }

        return ApplyEnvironmentOverrides(config);
    }

    private static ClawSharpConfig ApplyEnvironmentOverrides(ClawSharpConfig config)
    {
        if (Environment.GetEnvironmentVariable("CLAWSHARP_DEFAULT_PROVIDER") is { } provider)
            config.DefaultProvider = provider;
        if (Environment.GetEnvironmentVariable("CLAWSHARP_DEFAULT_MODEL") is { } model)
            config.DefaultModel = model;
        if (Environment.GetEnvironmentVariable("OPENAI_API_KEY") is { } oaiKey)
            (config.Providers.Openai ??= new ProviderEntry()).ApiKey = oaiKey;
        if (Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") is { } antKey)
            (config.Providers.Anthropic ??= new ProviderEntry()).ApiKey = antKey;

        return config;
    }

    private static string ToSnakeCase(string name)
    {
        var result = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0) result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }
}
