using System.Runtime.InteropServices;

namespace ClawSharp.Cli.Commands;

/// <summary>
/// Utility class for generating and managing system service units.
/// </summary>
public static class ServiceInstaller
{
    private const string ServiceName = "clawsharp";
    private const string ServiceLabel = "com.clawsharp.gateway";

    /// <summary>
    /// Generates a systemd service unit file content.
    /// </summary>
    public static string GenerateSystemdUnit(string executablePath, string workingDirectory)
    {
        return @"[Unit]
Description=ClawSharp Gateway Service
After=network.target

[Service]
Type=simple
User=" + Environment.UserName + @"
WorkingDirectory=" + workingDirectory + @"
ExecStart=" + executablePath + @" gateway
Restart=always
RestartSec=10
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target";
    }

    /// <summary>
    /// Generates a macOS launchd plist file content.
    /// </summary>
    public static string GenerateLaunchdPlist(string executablePath, string workingDirectory)
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>" + ServiceLabel + @"</string>
    <key>ProgramArguments</key>
    <array>
        <string>" + executablePath + @"</string>
        <string>gateway</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>WorkingDirectory</key>
    <string>" + workingDirectory + @"</string>
    <key>StandardOutPath</key>
    <string>" + workingDirectory + @"/logs/stdout.log</string>
    <key>StandardErrorPath</key>
    <string>" + workingDirectory + @"/logs/stderr.log</string>
</dict>
</plist>";
    }

    /// <summary>
    /// Gets the platform-appropriate service unit path.
    /// </summary>
    public static string GetServiceUnitPath(OSPlatform platform, bool userMode)
    {
        if (platform == OSPlatform.Linux)
        {
            if (userMode)
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, ".config/systemd/user", ServiceName + ".service");
            }
            return "/etc/systemd/system/" + ServiceName + ".service";
        }

        if (platform == OSPlatform.OSX)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dir = userMode ? "LaunchAgents" : "LaunchDaemons";
            return Path.Combine(home, "Library/Application Support", dir, ServiceLabel + ".plist");
        }

        throw new PlatformNotSupportedException("Platform " + platform + " is not supported for service installation.");
    }

    /// <summary>
    /// Detects the current operating system platform.
    /// </summary>
    public static OSPlatform DetectPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return OSPlatform.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return OSPlatform.OSX;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return OSPlatform.Windows;

        return OSPlatform.Linux; // Default to Linux
    }
}
