using GrifballWebApp.Domain.Archive;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>The historical archive's rows for tests, made by their own methods.</summary>
internal static class HistoricalTestData
{
    /// <summary>An enabled source, as a catalog entry would make it.</summary>
    public static HistoricalSource Source(string id = "test-source", string url = "https://example.org/", string? config = null,
        HistoricalSourceKind kind = HistoricalSourceKind.GoogleSheet, string? sheetKey = null, string? waybackTimestamp = null,
        string category = "Test", string? title = null)
        => HistoricalSource.FromCatalog(id, new HistoricalSourceListing
        {
            Category = category,
            Title = title ?? id,
            Url = url,
            Kind = kind,
            SheetKey = sheetKey,
            WaybackTimestamp = waybackTimestamp,
            Config = config,
        }, enabled: true);
}
