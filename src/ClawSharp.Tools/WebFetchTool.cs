using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClawSharp.Core.Tools;

namespace ClawSharp.Tools;

/// <summary>
/// Tool for fetching and extracting readable content from web pages.
/// </summary>
public class WebFetchTool : ITool
{
    private readonly HttpClient _httpClient;
    private const int DefaultMaxChars = 50_000;

    public string Name => "web_fetch";

    public string Description => "Fetch a web page and extract its readable content. Strips scripts, styles, and navigation elements.";

    public ToolSpec Specification => new(
        Name: Name,
        Description: Description,
        ParametersSchema: JsonSerializer.Deserialize<JsonElement>(@"
        {
            ""type"": ""object"",
            ""properties"": {
                ""url"": {
                    ""type"": ""string"",
                    ""description"": ""The URL to fetch""
                },
                ""max_chars"": {
                    ""type"": ""integer"",
                    ""description"": ""Maximum characters to return (default: 50000)""
                }
            },
            ""required"": [""url""]
        }")
    );

    public WebFetchTool(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken ct = default)
    {
        // Extract URL
        if (!arguments.TryGetProperty("url", out var urlElement))
        {
            return new ToolResult(false, "", "Missing required parameter: url");
        }

        var urlString = urlElement.GetString();
        if (string.IsNullOrWhiteSpace(urlString))
        {
            return new ToolResult(false, "", "URL cannot be empty");
        }

        // Validate URL
        if (!Uri.TryCreate(urlString, UriKind.Absolute, out var uri))
        {
            return new ToolResult(false, "", "Invalid URL format");
        }

        // Extract max_chars (optional)
        var maxChars = DefaultMaxChars;
        if (arguments.TryGetProperty("max_chars", out var maxCharsElement))
        {
            maxChars = maxCharsElement.GetInt32();
            if (maxChars < 1) maxChars = DefaultMaxChars;
        }

        try
        {
            // Fetch content
            var response = await _httpClient.GetAsync(uri, ct);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(ct);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

            // Extract readable content based on content type
            string extractedContent;
            if (contentType.Contains("text/html"))
            {
                extractedContent = ExtractHtmlContent(content);
            }
            else
            {
                extractedContent = content;
            }

            // Truncate if needed
            if (extractedContent.Length > maxChars)
            {
                extractedContent = extractedContent.Substring(0, maxChars) + "\n\n[Content truncated - exceeded max_chars limit]";
            }

            return new ToolResult(true, extractedContent);
        }
        catch (HttpRequestException ex)
        {
            return new ToolResult(false, "", $"HTTP error fetching URL: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new ToolResult(false, "", $"Error fetching URL: {ex.Message}");
        }
    }

    private static string ExtractHtmlContent(string html)
    {
        // Remove script tags and their content
        html = Regex.Replace(html, @"<script[^>]*>.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // Remove style tags and their content
        html = Regex.Replace(html, @"<style[^>]*>.*?</style>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // Remove common navigation/header/footer elements
        html = Regex.Replace(html, @"<nav[^>]*>.*?</nav>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<header[^>]*>.*?</header>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<footer[^>]*>.*?</footer>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // Remove all remaining HTML tags
        html = Regex.Replace(html, @"<[^>]+>", " ");

        // Decode HTML entities
        html = System.Net.WebUtility.HtmlDecode(html);

        // Clean up whitespace
        html = Regex.Replace(html, @"\s+", " ");
        html = html.Trim();

        // If the content is very short or empty, return it as-is
        if (html.Length < 50)
        {
            return html;
        }

        // Add some basic structure back for longer content
        var lines = html.Split(new[] { ". " }, StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 5) // Filter out very short fragments
            {
                result.AppendLine(trimmed.EndsWith('.') ? trimmed : trimmed + ".");
            }
        }

        return result.ToString().Trim();
    }
}
