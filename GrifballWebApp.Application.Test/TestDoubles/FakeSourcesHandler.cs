using System.Net;
using System.Text;

namespace GrifballWebApp.Application.Test;

/// <summary>Answers each request from the test (never the network) and records what was asked.</summary>
internal sealed class FakeSourcesHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<string> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        lock (Requests)
            Requests.Add(request.RequestUri!.ToString());
        var response = respond(request);
        response.RequestMessage ??= request;
        return Task.FromResult(response);
    }

    public static HttpResponseMessage Ok(byte[] content, string type)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };
        if (type.Length > 0)
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(type);
        return response;
    }

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status) { Content = new StringContent("<html>no</html>", Encoding.UTF8, "text/html") };
}
