using System.Net;
using System.Text;
using Google.Apis.Sheets.v4;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// Records Google Sheets API requests and returns canned JSON. Shared with GrifballWebApp.Integration.Test, which
/// references this project.
/// </summary>
internal sealed class FakeSheetsHandler : HttpMessageHandler
{
    public List<(HttpMethod Method, string Url, string Body)> Requests { get; } = new();
    public Func<HttpRequestMessage, string> Respond { get; set; } = _ => "{}";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, Uri.UnescapeDataString(request.RequestUri!.ToString()), body));
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Respond(request), Encoding.UTF8, "application/json"),
        };
    }
}

internal sealed class FakeSheetsHttpClientFactory : Google.Apis.Http.HttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    public FakeSheetsHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
    protected override HttpMessageHandler CreateHandler(Google.Apis.Http.CreateHttpClientArgs args) => _handler;

    /// <summary>A real SheetsService whose requests all go to <paramref name="handler"/>: no credential, no network.</summary>
    public static SheetsService SheetsServiceOn(HttpMessageHandler handler) => new(new Google.Apis.Services.BaseClientService.Initializer
    {
        HttpClientFactory = new FakeSheetsHttpClientFactory(handler),
        ApplicationName = "tests",
        GZipEnabled = false,
    });
}
