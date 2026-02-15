using System.Text.Json;
using ClawSharp.Core.Security;
using ClawSharp.Core.Tools;

namespace ClawSharp.Tools;

/// <summary>
/// Tool for reading file contents with optional offset and limit.
/// </summary>
public class FileReadTool : ITool
{
    private readonly ISecurityPolicy _securityPolicy;

    public string Name => "read_file";

    public string Description => "Read the contents of a file. Supports optional offset and limit parameters for reading partial content.";

    public ToolSpec Specification => new(
        Name: Name,
        Description: Description,
        ParametersSchema: JsonSerializer.Deserialize<JsonElement>(@"
        {
            ""type"": ""object"",
            ""properties"": {
                ""path"": {
                    ""type"": ""string"",
                    ""description"": ""The path to the file to read""
                },
                ""offset"": {
                    ""type"": ""integer"",
                    ""description"": ""Line number to start reading from (1-indexed)""
                },
                ""limit"": {
                    ""type"": ""integer"",
                    ""description"": ""Maximum number of lines to read""
                }
            },
            ""required"": [""path""]
        }")
    );

    public FileReadTool(ISecurityPolicy securityPolicy)
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

        // Check security policy
        if (!_securityPolicy.IsPathAllowed(path))
        {
            return new ToolResult(false, "", $"Path not allowed by security policy: {path}");
        }

        // Check file exists
        if (!File.Exists(path))
        {
            return new ToolResult(false, "", $"File not found: {path}");
        }

        try
        {
            // Read all lines
            var lines = await File.ReadAllLinesAsync(path, ct);

            // Parse offset and limit
            var offset = 0;
            var limit = lines.Length;

            if (arguments.TryGetProperty("offset", out var offsetElement))
            {
                offset = offsetElement.GetInt32() - 1; // Convert to 0-indexed
                if (offset < 0) offset = 0;
            }

            if (arguments.TryGetProperty("limit", out var limitElement))
            {
                limit = limitElement.GetInt32();
            }

            // Clamp values
            if (offset >= lines.Length)
            {
                return new ToolResult(true, "");
            }

            var count = Math.Min(limit, lines.Length - offset);
            var selectedLines = lines.Skip(offset).Take(count);

            return new ToolResult(true, string.Join(Environment.NewLine, selectedLines));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, "", $"Error reading file: {ex.Message}");
        }
    }
}
