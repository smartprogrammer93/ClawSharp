using System.CommandLine;
using System.Runtime.InteropServices;

namespace ClawSharp.Cli.Commands;

/// <summary>
/// Command for managing the ClawSharp system service (systemd/launchd).
/// </summary>
public class ServiceCommand : Command
{
    public ServiceCommand() : base("service", "Manage ClawSharp system service")
    {
        var installCommand = new Command("install", "Install the service");
        var uninstallCommand = new Command("uninstall", "Uninstall the service");
        var statusCommand = new Command("status", "Check service status");

        installCommand.SetAction(_ => ExecuteInstall());
        uninstallCommand.SetAction(_ => ExecuteUninstall());
        statusCommand.SetAction(_ => ExecuteStatus());

        Add(installCommand);
        Add(uninstallCommand);
        Add(statusCommand);
    }

    private static void ExecuteInstall()
    {
        Console.WriteLine("  Installing ClawSharp service...");

        var platform = ServiceInstaller.DetectPlatform();
        var executablePath = FindExecutablePath();
        var workingDir = FindWorkingDirectory();
        var userMode = !IsRunningAsRoot();

        try
        {
            var unitPath = ServiceInstaller.GetServiceUnitPath(platform, userMode);
            Directory.CreateDirectory(Path.GetDirectoryName(unitPath)!);

            string unitContent;
            if (platform == OSPlatform.Linux)
            {
                unitContent = ServiceInstaller.GenerateSystemdUnit(executablePath, workingDir);
            }
            else if (platform == OSPlatform.OSX)
            {
                unitContent = ServiceInstaller.GenerateLaunchdPlist(executablePath, workingDir);
            }
            else
            {
                Console.WriteLine("  ❌ Service installation not supported on this platform.");
                return;
            }

            File.WriteAllText(unitPath, unitContent);
            Console.WriteLine($"  ✅ Service unit written to: {unitPath}");

            if (platform == OSPlatform.Linux && !userMode)
            {
                Console.WriteLine("  To enable and start the service, run:");
                Console.WriteLine($"    sudo systemctl daemon-reload");
                Console.WriteLine($"    sudo systemctl enable clawsharp");
                Console.WriteLine($"    sudo systemctl start clawsharp");
            }
            else if (platform == OSPlatform.OSX)
            {
                Console.WriteLine("  To load the service, run:");
                Console.WriteLine($"    launchctl load {unitPath}");
            }
            else if (userMode)
            {
                Console.WriteLine("  To enable and start the service, run:");
                Console.WriteLine($"    systemctl --user daemon-reload");
                Console.WriteLine($"    systemctl --user enable clawsharp");
                Console.WriteLine($"    systemctl --user start clawsharp");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Failed to install service: {ex.Message}");
        }
    }

    private static void ExecuteUninstall()
    {
        Console.WriteLine("  Uninstalling ClawSharp service...");

        var platform = ServiceInstaller.DetectPlatform();
        var userMode = !IsRunningAsRoot();

        try
        {
            var unitPath = ServiceInstaller.GetServiceUnitPath(platform, userMode);

            if (File.Exists(unitPath))
            {
                File.Delete(unitPath);
                Console.WriteLine($"  ✅ Service unit removed: {unitPath}");
            }
            else
            {
                Console.WriteLine($"  ℹ️  No service unit found at: {unitPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Failed to uninstall service: {ex.Message}");
        }
    }

    private static void ExecuteStatus()
    {
        Console.WriteLine("  Checking ClawSharp service status...");

        var platform = ServiceInstaller.DetectPlatform();
        var userMode = !IsRunningAsRoot();
        var unitPath = ServiceInstaller.GetServiceUnitPath(platform, userMode);

        if (!File.Exists(unitPath))
        {
            Console.WriteLine($"  ❌ Service not installed (unit file not found: {unitPath})");
            return;
        }

        Console.WriteLine($"  ✅ Service installed: {unitPath}");

        if (platform == OSPlatform.Linux)
        {
            Console.WriteLine();
            Console.WriteLine("  System service status (requires systemctl):");
            var systemctlCmd = userMode ? "systemctl --user status clawsharp" : "sudo systemctl status clawsharp";
            Console.WriteLine($"    {systemctlCmd}");
        }
        else if (platform == OSPlatform.OSX)
        {
            Console.WriteLine();
            Console.WriteLine("  Launchd service status (requires launchctl):");
            Console.WriteLine($"    launchctl list | grep clawsharp");
        }
    }

    private static string FindExecutablePath()
    {
        // Try to find dotclaw/clawsharp executable
        var currentExe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(currentExe))
        {
            return currentExe;
        }

        // Fallback to common paths
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "/usr/local/bin/clawsharp";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "/usr/local/bin/clawsharp";
        }

        return "clawsharp";
    }

    private static string FindWorkingDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".clawsharp");
    }

    private static bool IsRunningAsRoot()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false; // Windows doesn't have the same concept
        }

        // Check if running as root via UID environment variable or by calling id command
        if (Environment.GetEnvironmentVariable("UID") == "0")
        {
            return true;
        }

        try
        {
            var procStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "id",
                Arguments = "-u",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(procStartInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return output == "0";
            }
        }
        catch
        {
            // If we can't determine, assume not root
        }

        return false;
    }
}
