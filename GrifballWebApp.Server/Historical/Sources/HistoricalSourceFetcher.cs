using GrifballWebApp.Server.Historical.Parsing;
using GrifballWebApp.Domain.Archive;

namespace GrifballWebApp.Server.Historical.Sources;

public sealed record FetchedContent(byte[] Content, string ContentType);

/// <summary>A download that didn't give the source's content (an error status, a sign-in page, nothing).</summary>
public sealed class HistoricalFetchException(string message) : Exception(message);

/// <summary>
/// Downloads a source the way it is published: a Google Sheets workbook as its xlsx export (one tab's gviz CSV when the
/// export is refused and the catalog names that tab), a page or channel at its URL, a Wayback capture at its permanent
/// URL. Anonymous, like a visitor: only what the sheets' owners share publicly.
/// </summary>
public class HistoricalSourceFetcher
{
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly HttpClient _http;

    public HistoricalSourceFetcher(HttpClient http)
    {
        _http = http;
    }

    public static string ExportUrl(string sheetKey) => $"https://docs.google.com/spreadsheets/d/{sheetKey}/export?format=xlsx";

    public static string CsvUrl(string sheetKey, string? gid) => $"https://docs.google.com/spreadsheets/d/{sheetKey}/gviz/tq?tqx=out:csv{(gid is null ? "" : $"&gid={gid}")}";

    public static string WaybackUrl(string timestamp, string url) => $"https://web.archive.org/web/{timestamp}id_/{url}";

    public async Task<FetchedContent> Fetch(HistoricalSource source, CancellationToken ct)
    {
        switch (source.Kind)
        {
            case HistoricalSourceKind.GoogleSheet:
                var key = source.SheetKey ?? throw new HistoricalFetchException("no sheet key");
                var (status, xlsx, _) = await Get(ExportUrl(key), ct);
                if (xlsx is not null && SheetWorkbook.IsZip(xlsx))
                    return new FetchedContent(xlsx, XlsxContentType);
                if (source.CsvSheetName is not null)
                    return await Csv(key, source.Gid, ct);
                throw new HistoricalFetchException($"the xlsx export answered {status}");
            case HistoricalSourceKind.GoogleSheetCsv:
                return await Csv(source.SheetKey ?? throw new HistoricalFetchException("no sheet key"), source.Gid, ct);
            case HistoricalSourceKind.Wayback:
                return await Page(WaybackUrl(source.WaybackTimestamp ?? throw new HistoricalFetchException("no capture timestamp"), source.Url), ct);
            default:
                return await Page(source.Url, ct);
        }
    }

    private async Task<FetchedContent> Csv(string sheetKey, string? gid, CancellationToken ct)
    {
        var (status, csv, type) = await Get(CsvUrl(sheetKey, gid), ct);
        // A private sheet answers with its sign-in page
        if (csv is null || csv.Length == 0 || type.Contains("html", StringComparison.OrdinalIgnoreCase))
            throw new HistoricalFetchException($"the CSV export answered {status}{(type.Length > 0 ? $" ({type})" : "")}");
        return new FetchedContent(csv, "text/csv");
    }

    private async Task<FetchedContent> Page(string url, CancellationToken ct)
    {
        var (status, content, type) = await Get(url, ct);
        if (content is null || content.Length == 0)
            throw new HistoricalFetchException($"answered {status}");
        return new FetchedContent(content, type.Length > 0 ? type : "text/html");
    }

    /// <summary>The status, the body when it succeeded (null otherwise) and its content type.</summary>
    private async Task<(int Status, byte[]? Content, string ContentType)> Get(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct);
        var type = response.Content.Headers.ContentType?.MediaType ?? "";
        // Google sends a private sheet's visitors to its sign-in page
        if (!response.IsSuccessStatusCode || response.RequestMessage?.RequestUri?.Host.StartsWith("accounts.", StringComparison.OrdinalIgnoreCase) == true)
            return ((int)response.StatusCode, null, type);
        return ((int)response.StatusCode, await response.Content.ReadAsByteArrayAsync(ct), type);
    }
}
