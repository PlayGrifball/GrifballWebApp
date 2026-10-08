using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Excel;
using GrifballWebApp.Server.Services;
using Google.Apis.Sheets.v4;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GrifballWebApp.Test;

/// <summary>Records Google Sheets API requests and returns canned JSON.</summary>
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
}
