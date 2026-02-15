using System.Net;
using System.Text;

namespace ClawSharp.TestHelpers;

/// <summary>
/// A mock HttpMessageHandler for testing HTTP-based providers.
/// Allows pre-configuring responses for specific request patterns.
/// </summary>
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly List<HttpRequestMessage> _sentRequests = [];

    /// <summary>All requests that were sent through this handler.</summary>
    public IReadOnlyList<HttpRequestMessage> SentRequests => _sentRequests;

    /// <summary>Enqueue a response to be returned for the next request.</summary>
    public MockHttpMessageHandler EnqueueResponse(HttpStatusCode statusCode, string content, string mediaType = "application/json")
    {
        _responses.Enqueue(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, mediaType),
        });
        return this;
    }

    /// <summary>Enqueue a successful JSON response.</summary>
    public MockHttpMessageHandler EnqueueJson(string json) =>
        EnqueueResponse(HttpStatusCode.OK, json);

    /// <summary>Enqueue an error response.</summary>
    public MockHttpMessageHandler EnqueueError(HttpStatusCode statusCode = HttpStatusCode.InternalServerError, string body = "{\"error\":\"test error\"}") =>
        EnqueueResponse(statusCode, body);

    /// <summary>Creates an HttpClient backed by this handler.</summary>
    public HttpClient CreateClient(string? baseAddress = null)
    {
        var client = new HttpClient(this);
        if (baseAddress is not null)
            client.BaseAddress = new Uri(baseAddress);
        return client;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _sentRequests.Add(request);

        if (_responses.Count == 0)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"error\":\"No mock response configured\"}", Encoding.UTF8, "application/json"),
            });
        }

        return Task.FromResult(_responses.Dequeue());
    }
}
