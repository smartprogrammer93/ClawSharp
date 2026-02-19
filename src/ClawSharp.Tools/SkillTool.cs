using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ClawSharp.Core.Tools;

namespace ClawSharp.Tools;

/// <summary>
/// A simple tool that executes a shell command.
/// Used for skill-defined tools.
/// </summary>
public class SkillTool : ITool
{
    private readonly string _command;

    public string Name { get; }
    public string Description { get; }
    public ToolSpec Specification { get; }

    public SkillTool(string name, string description, string command)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        _command = command ?? throw new ArgumentNullException(nameof(command));

        Specification = new ToolSpec(
            Name: Name,
            Description: Description,
            ParametersSchema: JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "args": {
                        "type": "string",
                        "description": "Arguments to pass to the command"
                    }
                }
            }
            """)
        );
    }

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken ct = default)
    {
        try
        {
            // Get optional args from arguments
            var args = "";
            if (arguments.TryGetProperty("args", out var argsElement))
            {
                args = argsElement.GetString() ?? "";
            }

            var fullCommand = string.IsNullOrEmpty(args) ? _command : $"{_command} {args}";

            var (exitCode, output, error) = await ExecuteCommandAsync(fullCommand, ct);

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
        cts.CancelAfter(TimeSpan.FromSeconds(30));

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

        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
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

        return (process.ExitCode, output.Trim(), error.Trim());
    }
}
