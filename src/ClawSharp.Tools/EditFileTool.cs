using System.Text.Json;
using ClawSharp.Core.Security;
using ClawSharp.Core.Tools;

namespace ClawSharp.Tools;

/// <summary>
/// Tool for editing a file by replacing exact text strings.
/// </summary>
public class EditFileTool : ITool
{
    private readonly ISecurityPolicy _securityPolicy;

    public string Name => "edit_file";

    public string Description => "Edit a file by replacing an exact text string. Returns an error if the old_string appears multiple times or is not found.";

    public ToolSpec Specification => new(
        Name: Name,
        Description: Description,
        ParametersSchema: JsonSerializer.Deserialize<JsonElement>(@"
        {
            ""type"": ""object"",
            ""properties"": {
                ""path"": {
                    ""type"": ""string"",
                    ""description"": ""The path to the file to edit""
                },
                ""old_string"": {
                    ""type"": ""string"",
                    ""description"": ""The exact text to find and replace""
                },
                ""new_string"": {
                    ""type"": ""string"",
                    ""description"": ""The text to replace old_string with""
                }
            },
            ""required"": [""path"", ""old_string"", ""new_string""]
        }")
    );

    public EditFileTool(ISecurityPolicy securityPolicy)
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

        // Extract old_string
        if (!arguments.TryGetProperty("old_string", out var oldStringElement))
        {
            return new ToolResult(false, "", "Missing required parameter: old_string");
        }

        var oldString = oldStringElement.GetString();
        if (string.IsNullOrEmpty(oldString))
        {
            return new ToolResult(false, "", "old_string cannot be empty");
        }

        // Extract new_string
        string newString = "";
        if (arguments.TryGetProperty("new_string", out var newStringElement))
        {
            newString = newStringElement.GetString() ?? "";
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
            // Read file content
            var content = await File.ReadAllTextAsync(path, ct);

            // Find occurrences
            var index = content.IndexOf(oldString, StringComparison.Ordinal);
            if (index < 0)
            {
                return new ToolResult(false, "", $"old_string not found in file: {oldString}");
            }

            // Check for multiple occurrences
            var nextIndex = content.IndexOf(oldString, index + oldString.Length, StringComparison.Ordinal);
            if (nextIndex >= 0)
            {
                var count = 1;
                while ((index = content.IndexOf(oldString, index + oldString.Length, StringComparison.Ordinal)) >= 0)
                {
                    count++;
                }
                return new ToolResult(false, "", $"old_string appears {count} times in file. Use a more specific string.");
            }

            // Replace
            var newContent = content.Substring(0, index) + newString + content.Substring(index + oldString.Length);

            // Write file
            await File.WriteAllTextAsync(path, newContent, ct);

            return new ToolResult(true, $"Successfully replaced text in {path}");
        }
        catch (Exception ex)
        {
            return new ToolResult(false, "", $"Error editing file: {ex.Message}");
        }
    }
}
