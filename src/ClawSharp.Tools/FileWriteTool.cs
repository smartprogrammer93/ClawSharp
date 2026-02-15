using System.Text.Json;
using ClawSharp.Core.Security;
using ClawSharp.Core.Tools;

namespace ClawSharp.Tools;

/// <summary>
/// Tool for writing content to a file, creating parent directories if needed.
/// </summary>
public class FileWriteTool : ITool
{
    private readonly ISecurityPolicy _securityPolicy;

    public string Name => "write_file";

    public string Description => "Write content to a file. Creates the file and parent directories if they don't exist.";

    public ToolSpec Specification => new(
        Name: Name,
        Description: Description,
        ParametersSchema: JsonSerializer.Deserialize<JsonElement>(@"
        {
            ""type"": ""object"",
            ""properties"": {
                ""path"": {
                    ""type"": ""string"",
                    ""description"": ""The path to the file to write""
                },
                ""content"": {
                    ""type"": ""string"",
                    ""description"": ""The content to write to the file""
                }
            },
            ""required"": [""path"", ""content""]
        }")
    );

    public FileWriteTool(ISecurityPolicy securityPolicy)
    {
        _securityPolicy = securityPolicy ?? throw new ArgumentNullException(nameof(securityPolicy));
    }

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken ct = default)
    {
        // Extract path
        if (!arguments.TryGetProperty("path", out var pathElement))
        {
            return new ToolResult(false, "", "Missing required parameter: path");
        }

        var path = pathElement.GetString();
        if (string.IsNullOrWhiteSpace(path))
        {
            return new ToolResult(false, "", "Path cannot be empty");
        }

        // Extract content
        if (!arguments.TryGetProperty("content", out var contentElement))
        {
            return new ToolResult(false, "", "Missing required parameter: content");
        }

        var content = contentElement.GetString() ?? "";

        // Check security policy
        if (!_securityPolicy.IsPathAllowed(path))
        {
            return new ToolResult(false, "", $"Path not allowed by security policy: {path}");
        }

        try
        {
            // Create parent directories if needed
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write file
            await File.WriteAllTextAsync(path, content, ct);

            return new ToolResult(true, $"Successfully wrote {content.Length} bytes to {path}");
        }
        catch (Exception ex)
        {
            return new ToolResult(false, "", $"Error writing file: {ex.Message}");
        }
    }
}
