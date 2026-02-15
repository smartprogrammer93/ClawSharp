using System.Net.Http.Headers;
using System.Text.Json;
using ClawSharp.Core.Tools;

namespace ClawSharp.Tools;

/// <summary>
/// Tool for searching the web using the Brave Search API.
/// </summary>
public class WebSearchTool : ITool
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public string Name => "web_search";

    public string Description => "Search the web using Brave Search API. Returns titles, URLs, and descriptions of search results.";

    public ToolSpec Specification => new(
        Name: Name,
        Description: Description,
        ParametersSchema: JsonSerializer.Deserialize<JsonElement>(@"
        {
            ""type"": ""object"",
            ""properties"": {
                ""query"": {
                    ""type"": ""string"",
                    ""description"": ""The search query""
                },
                ""count"": {
                    ""type"": ""integer"",
                    ""description"": ""Number of results to return (1-10)"",
                    ""default"": 5
                }
            },
            ""required"": [""query""]
        }")
    );

    public WebSearchTool(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
    }

    public async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken ct = default)
    {
        // Extract query
        if (!arguments.TryGetProperty("query", out var queryElement))
        {
            return new ToolResult(false, "", "Missing required parameter: query");
        }

        var query = queryElement.GetString();
        if (string.IsNullOrWhiteSpace(query))
        {
            return new ToolResult(false, "", "Query cannot be empty");
        }

        // Extract count (optional)
        var count = 5;
        if (arguments.TryGetProperty("count", out var countElement))
        {
            count = countElement.GetInt32();
            if (count < 1) count = 1;
            if (count > 10) count = 10;
        }

        try
        {
            // Build request URL
            var requestUri = $"res/v1/web/search?q={Uri.EscapeDataString(query)}&count={count}";

            // Create request with API key header
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("X-Subscription-Token", _apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Send request
            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                return new ToolResult(false, "", $"Search API error: {(int)response.StatusCode} ({response.StatusCode}) - {await response.Content.ReadAsStringAsync(ct)}");
            }

            // Parse response
            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);

            var results = new List<string>();
            if (doc.RootElement.TryGetProperty("web", out var web) &&
                web.TryGetProperty("results", out var resultsArray))
            {
                foreach (var result in resultsArray.EnumerateArray())
                {
                    var title = result.TryGetProperty("title", out var t) ? t.GetString() : "No title";
                    var url = result.TryGetProperty("url", out var u) ? u.GetString() : "";
                    var description = result.TryGetProperty("description", out var d) ? d.GetString() : "";

                    results.Add($"**{title}**\n{url}\n{description}\n");
                }
            }

            if (results.Count == 0)
            {
                return new ToolResult(true, "No results found.");
            }

            return new ToolResult(true, string.Join("\n", results));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, "", $"Error searching web: {ex.Message}");
        }
    }
}
