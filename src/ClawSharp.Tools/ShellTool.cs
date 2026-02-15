using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ClawSharp.Core.Security;
using ClawSharp.Core.Tools;

namespace ClawSharp.Tools;

/// <summary>
/// Tool for executing shell commands with security policy enforcement.
/// </summary>
public class ShellTool : ITool
{
    private readonly ISecurityPolicy _securityPolicy;
    private readonly TimeSpan _timeout;
    private const int MaxOutputBytes = 10_240; // 10KB

    public string Name => "shell";
    
    public string Description => "Execute a shell command and return its output. Use this to interact with the system, run programs, check dates, list files, etc.";

    public ToolSpec Specification => new(
        Name: "shell",
        Description: Description,
        ParametersSchema: JsonSerializer.Deserialize<JsonElement>("""
        {
            "type": "object",
            "properties": {
                "command": {
                    "type": "string",
                    "description": "The shell command to execute"
                }
            },
            "required": ["command"]
        }
        """)
    );

    public ShellTool(ISecurityPolicy securityPolicy, TimeSpan? timeout = null)
    {
        _securityPolicy = securityPolicy ?? throw new ArgumentNullException(nameof(securityPolicy));
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken ct = default)
    {
        // Extract command from arguments
        if (!arguments.TryGetProperty("command", out var commandElement))
        {
            return new ToolResult(false, "", "Missing required parameter: command");
        }

        var command = commandElement.GetString();
        if (string.IsNullOrWhiteSpace(command))
        {
            return new ToolResult(false, "", "Command cannot be empty");
        }

        // Check security policy
        if (!_securityPolicy.IsCommandAllowed(command))
        {
            return new ToolResult(false, "", $"Command not allowed by security policy: {command}");
        }

        // Execute command
        try
        {
            var (exitCode, output, error) = await ExecuteCommandAsync(command, ct);

            if (exitCode != 0)
            {
                var errorMessage = $"Command exited with exit code {exitCode}";
                if (!string.IsNullOrWhiteSpace(error))
                    errorMessage += $"\nStderr: {error}";
                return new ToolResult(false, output, errorMessage);
            }

            return new ToolResult(true, output);
        }
        catch (OperationCanceledException)
        {
            return new ToolResult(false, "", "Command execution timed out");
        }
        catch (Exception ex)
        {
            return new ToolResult(false, "", $"Error executing command: {ex.Message}");
        }
    }

    private async Task<(int ExitCode, string Output, string Error)> ExecuteCommandAsync(string command, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        var processInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processInfo };
        process.Start();

        // Read output and error with size limits
        var outputTask = ReadStreamWithLimitAsync(process.StandardOutput, MaxOutputBytes, cts.Token);
        var errorTask = ReadStreamWithLimitAsync(process.StandardError, MaxOutputBytes, cts.Token);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Timeout - kill the process
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Process might have already exited
            }
            throw new OperationCanceledException("Command execution timed out");
        }

        var output = await outputTask;
        var error = await errorTask;

        // Combine stderr into output if there's no error
        if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(error))
        {
            output = string.IsNullOrWhiteSpace(output) ? error : $"{output}\n{error}";
            error = "";
        }

        return (process.ExitCode, output, error);
    }

    private async Task<string> ReadStreamWithLimitAsync(StreamReader reader, int maxBytes, CancellationToken ct)
    {
        var builder = new StringBuilder();
        var bytesRead = 0;
        var truncated = false;

        while (true)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null)
                break;

            var lineBytes = Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;

            if (bytesRead + lineBytes <= maxBytes)
            {
                builder.AppendLine(line);
                bytesRead += lineBytes;
            }
            else if (!truncated)
            {
                builder.AppendLine("\n[Output truncated - exceeded 10KB limit]");
                truncated = true;
                // Continue reading to drain the stream, but don't add more
            }
        }

        return builder.ToString().TrimEnd();
    }
}
