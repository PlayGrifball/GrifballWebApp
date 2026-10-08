using System.Net;
using System.Text;
using System.Text.Json;
using NetCord.Rest;

namespace GrifballWebApp.Test;

/// <summary>
/// Fake NetCord request handler so tests never reach Discord. Records every request and answers
/// DELETE (and every request when <see cref="NoContentForAll"/> is set) with 204, anything else with a canned message JSON.
/// </summary>
internal sealed class FakeRestRequestHandler : IRestRequestHandler
{
    public record RecordedRequest(HttpMethod Method, string Path, string? Body)
    {
        public JsonDocument Json => JsonDocument.Parse(Body ?? "{}");
    }

    private readonly List<RecordedRequest> _requests = [];

    /// <summary>Answer every request with 204 No Content.</summary>
    public bool NoContentForAll { get; init; }

    public IReadOnlyList<RecordedRequest> Requests
    {
        get { lock (_requests) return _requests.ToList(); }
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        string? body = null;
        if (request.Content is MultipartContent multipart)
        {
            // NetCord sends messages as multipart/form-data with the JSON in the "payload_json" part
            foreach (var part in multipart)
            {
                var name = part.Headers.ContentDisposition?.Name?.Trim('"');
                if (name == "payload_json" || body is null)
                    body = await part.ReadAsStringAsync(cancellationToken);
                if (name == "payload_json")
                    break;
            }
        }
        else if (request.Content is not null)
        {
            body = await request.Content.ReadAsStringAsync(cancellationToken);
        }
        lock (_requests)
            _requests.Add(new RecordedRequest(request.Method, request.RequestUri!.AbsolutePath, body));

        if (NoContentForAll || request.Method == HttpMethod.Delete)
            return new HttpResponseMessage(HttpStatusCode.NoContent);

        var channelId = "1";
        var segments = request.RequestUri!.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var idx = Array.IndexOf(segments, "channels");
        if (idx >= 0 && idx + 1 < segments.Length)
            channelId = segments[idx + 1];

        var json = $$"""
        {"id":"1000","channel_id":"{{channelId}}","author":{"id":"2000","username":"bot","discriminator":"0","global_name":null,"avatar":null},
         "content":"","timestamp":"2024-01-01T00:00:00+00:00","edited_timestamp":null,"tts":false,"mention_everyone":false,
         "mentions":[],"mention_roles":[],"attachments":[],"embeds":[],"pinned":false,"type":0}
        """;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    public void AddDefaultHeader(string name, IEnumerable<string> values) { }

    public void Dispose() { }

    /// <summary>Messages posted to a channel, as (channelId, parsed JSON body).</summary>
    public List<(ulong ChannelId, JsonElement Body)> SentMessages()
    {
        return Requests
            .Where(r => r.Method == HttpMethod.Post && r.Path.Contains("/channels/") && r.Path.EndsWith("/messages"))
            .Select(r =>
            {
                var segs = r.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var ch = ulong.Parse(segs[Array.IndexOf(segs, "channels") + 1]);
                return (ch, JsonDocument.Parse(r.Body ?? "{}").RootElement.Clone());
            })
            .ToList();
    }

    public static RestClient CreateClient(out FakeRestRequestHandler handler)
    {
        handler = new FakeRestRequestHandler();
        return new RestClient(new RestClientConfiguration { RequestHandler = handler });
    }
}
