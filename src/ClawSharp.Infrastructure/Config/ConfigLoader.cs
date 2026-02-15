using ClawSharp.Core.Config;
using Tomlyn;

namespace ClawSharp.Infrastructure.Config;

/// <summary>
/// Loads ClawSharp configuration from TOML files.
/// </summary>
public static class ConfigLoader
{
    /// <summary>
    /// Loads configuration from ~/.clawsharp/config.toml
    /// Falls back to default configuration if file doesn't exist.
    /// </summary>
    public static ClawSharpConfig LoadConfig(string? path = null)
    {
        var configPath = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".clawsharp",
            "config.toml"
        );

        if (!File.Exists(configPath))
        {
            // Return default config
            return new ClawSharpConfig();
        }

        var toml = File.ReadAllText(configPath);
        var config = Toml.ToModel<ClawSharpConfig>(toml);

        // Expand tilde in paths
        config.WorkspaceDir = ExpandPath(config.WorkspaceDir);
        config.DataDir = ExpandPath(config.DataDir);

        return config;
    }

    private static string ExpandPath(string path)
    {
        if (path.StartsWith("~/") || path.StartsWith("~\\"))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path.Substring(2)
            );
        }
        return path;
    }
}
