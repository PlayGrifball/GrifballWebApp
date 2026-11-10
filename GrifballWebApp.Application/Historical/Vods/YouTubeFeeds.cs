using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace GrifballWebApp.Application.Historical.Vods;

/// <summary>A video as one of YouTube's pages or APIs gives it: what's missing there is null.</summary>
public sealed record YouTubeVideo
{
    public required string VideoID { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public DateTime? PublishedAt { get; init; }
    /// <summary>Worked out from a "2w ago" rather than given.</summary>
    public bool PublishedAtIsApproximate { get; init; }
    public int? DurationSeconds { get; init; }
    public string? ChannelID { get; init; }
    /// <summary>A Short (a clip, not a VOD): never kept.</summary>
    public bool IsShort { get; init; }

    /// <summary>This video with what <paramref name="other"/> knows better: an exact date over an approximate one, a
    /// description, a duration.</summary>
    public YouTubeVideo Merge(YouTubeVideo other) => this with
    {
        Title = other.Title ?? Title,
        Description = other.Description ?? Description,
        PublishedAt = PublishedAt is null || (PublishedAtIsApproximate && other.PublishedAt is not null && !other.PublishedAtIsApproximate) ? other.PublishedAt : PublishedAt,
        PublishedAtIsApproximate = PublishedAt is null || (PublishedAtIsApproximate && other.PublishedAt is not null && !other.PublishedAtIsApproximate) ? other.PublishedAtIsApproximate : PublishedAtIsApproximate,
        DurationSeconds = other.DurationSeconds ?? DurationSeconds,
        ChannelID = other.ChannelID ?? ChannelID,
        IsShort = IsShort || other.IsShort,
    };
}

/// <summary>
/// Reads YouTube's pages without an API key: a channel's uploads feed (videos.xml, the latest ~15 with exact dates and
/// descriptions) and a channel page's ytInitialData (the videos it lists, with their durations and "2w ago").
/// </summary>
public static partial class YouTubeFeeds
{
    // XML namespace names, which are identifiers matched as written, never fetched: the feed's own are http
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom"; // NOSONAR S5332: a namespace name, not a URL that is requested
    private static readonly XNamespace Yt = "http://www.youtube.com/xml/schemas/2015"; // NOSONAR S5332: a namespace name, not a URL that is requested
    private static readonly XNamespace Media = "http://search.yahoo.com/mrss/";

    public static string FeedUrl(string channelId) => $"https://www.youtube.com/feeds/videos.xml?channel_id={Uri.EscapeDataString(channelId)}";

    /// <summary>The channel ID of a youtube.com/channel/UC... link, or null.</summary>
    public static string? ChannelIdFromUrl(string url) => ChannelUrl().Match(url) is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>The feed's videos. Throws <see cref="XmlException"/> on something that isn't one.</summary>
    public static List<YouTubeVideo> ParseFeed(string xml)
    {
        var doc = XDocument.Parse(xml);
        var videos = new List<YouTubeVideo>();
        foreach (var entry in doc.Root?.Elements(Atom + "entry") ?? [])
        {
            var id = entry.Element(Yt + "videoId")?.Value;
            if (string.IsNullOrWhiteSpace(id))
                continue;
            var link = entry.Elements(Atom + "link").FirstOrDefault(l => (string?)l.Attribute("rel") == "alternate")?.Attribute("href")?.Value ?? "";
            var published = entry.Element(Atom + "published")?.Value;
            videos.Add(new YouTubeVideo
            {
                VideoID = id.Trim(),
                Title = entry.Element(Atom + "title")?.Value.Trim(),
                Description = NullIfBlank(entry.Element(Media + "group")?.Element(Media + "description")?.Value),
                PublishedAt = DateTimeOffset.TryParse(published, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at.UtcDateTime : null,
                ChannelID = entry.Element(Yt + "channelId")?.Value.Trim(),
                IsShort = link.Contains("/shorts/", StringComparison.OrdinalIgnoreCase),
            });
        }
        return videos;
    }

    /// <summary>
    /// The videos a channel page lists (its ytInitialData: the lockups of today's layout, the videoRenderers of the older
    /// one), their dates worked out from "2w ago" against <paramref name="fetchedAt"/>. Shorts and playlists are left out.
    /// A page without ytInitialData gives none.
    /// </summary>
    public static List<YouTubeVideo> ParseChannelPage(string html, DateTime fetchedAt, string? channelId)
    {
        var start = html.IndexOf("ytInitialData = ", StringComparison.Ordinal);
        if (start < 0)
            return [];
        start = html.IndexOf('{', start);
        var end = start < 0 ? -1 : html.IndexOf(";</script>", start, StringComparison.Ordinal);
        if (end < 0)
            return [];
        JsonDocument doc;
        try
        {
            // Nested deeper than the default 64
            doc = JsonDocument.Parse(html.AsMemory(start, end - start), new JsonDocumentOptions { MaxDepth = 512 });
        }
        catch (JsonException)
        {
            return [];
        }
        using (doc)
        {
            var videos = new Dictionary<string, YouTubeVideo>();
            Walk(doc.RootElement, fetchedAt, channelId, videos);
            return videos.Values.ToList();
        }
    }

    private static void Walk(JsonElement element, DateTime fetchedAt, string? channelId, Dictionary<string, YouTubeVideo> videos)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                Walk(item, fetchedAt, channelId, videos);
            return;
        }
        if (element.ValueKind != JsonValueKind.Object)
            return;
        foreach (var property in element.EnumerateObject())
        {
            var video = property.Name switch
            {
                "lockupViewModel" => Lockup(property.Value, fetchedAt, channelId),
                "videoRenderer" or "gridVideoRenderer" => Renderer(property.Value, fetchedAt, channelId),
                _ => null,
            };
            if (video is not null)
                videos.TryAdd(video.VideoID, video);
            else
                Walk(property.Value, fetchedAt, channelId, videos);
        }
    }

    private static YouTubeVideo? Lockup(JsonElement lockup, DateTime fetchedAt, string? channelId)
    {
        if (Str(lockup, "contentType") != "LOCKUP_CONTENT_TYPE_VIDEO" || Str(lockup, "contentId") is not { } id)
            return null;
        var metadata = Path(lockup, "metadata", "lockupMetadataViewModel");
        var title = metadata is { } m ? Str(Path(m, "title") ?? default, "content") : null;
        // The metadata parts are the views and the "2w ago"; the badge on the thumbnail is the duration
        var texts = Strings(lockup, "content").Concat(Strings(lockup, "accessibilityLabel")).ToList();
        return new YouTubeVideo
        {
            VideoID = id,
            Title = title,
            PublishedAt = texts.Select(t => Ago(t, fetchedAt)).FirstOrDefault(d => d is not null),
            PublishedAtIsApproximate = true,
            DurationSeconds = Strings(lockup, "text").Select(Clock).FirstOrDefault(d => d is not null),
            ChannelID = channelId,
        };
    }

    private static YouTubeVideo? Renderer(JsonElement renderer, DateTime fetchedAt, string? channelId)
    {
        if (Str(renderer, "videoId") is not { } id)
            return null;
        var title = Path(renderer, "title") is { } t ? Str(t, "simpleText") ?? Strings(t, "text").FirstOrDefault() : null;
        return new YouTubeVideo
        {
            VideoID = id,
            Title = title,
            PublishedAt = Path(renderer, "publishedTimeText") is { } p && Str(p, "simpleText") is { } ago ? Ago(ago, fetchedAt) : null,
            PublishedAtIsApproximate = true,
            DurationSeconds = Path(renderer, "lengthText") is { } l && Str(l, "simpleText") is { } length ? Clock(length) : null,
            ChannelID = channelId,
        };
    }

    /// <summary>"2w ago", "4mo ago", "2 weeks ago", "Streamed 3 days ago": that long before <paramref name="now"/>.</summary>
    public static DateTime? Ago(string text, DateTime now)
    {
        var m = AgoPattern().Match(text);
        if (!m.Success)
            return null;
        var n = int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture);
        var unit = m.Groups["unit"].Value.ToLowerInvariant();
        // "mo" (months) before "m" (minutes); the rest by their first letter
        if (unit.StartsWith("mo", StringComparison.Ordinal))
            return now.AddMonths(-n);
        return unit[0] switch
        {
            's' => now.AddSeconds(-n),
            'm' => now.AddMinutes(-n),
            'h' => now.AddHours(-n),
            'd' => now.AddDays(-n),
            'w' => now.AddDays(-7 * n),
            _ => now.AddYears(-n),
        };
    }

    /// <summary>"1:50:01" or "12:34" in seconds.</summary>
    public static int? Clock(string? text)
    {
        if (text is null || !ClockPattern().IsMatch(text.Trim()))
            return null;
        return text.Trim().Split(':').Aggregate(0, (total, part) => total * 60 + int.Parse(part, CultureInfo.InvariantCulture));
    }

    /// <summary>An ISO 8601 duration (the Data API's "PT1H50M1S") in seconds.</summary>
    public static int? IsoDuration(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        try
        {
            return (int)XmlConvert.ToTimeSpan(text).TotalSeconds;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static JsonElement? Path(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out element))
                return null;
        }
        return element;
    }

    private static string? Str(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Every string property called <paramref name="name"/> under <paramref name="element"/>, in document order.</summary>
    private static IEnumerable<string> Strings(JsonElement element, string name) => element.ValueKind switch
    {
        JsonValueKind.Array => element.EnumerateArray().SelectMany(item => Strings(item, name)),
        JsonValueKind.Object => element.EnumerateObject().SelectMany(property => property.Name == name && property.Value.ValueKind == JsonValueKind.String
            ? new[] { property.Value.GetString()! }
            : Strings(property.Value, name)),
        _ => Enumerable.Empty<string>(),
    };

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"youtube\.com/channel/(UC[\w-]{22})", RegexOptions.IgnoreCase)]
    private static partial Regex ChannelUrl();

    [GeneratedRegex(@"\b(?<n>\d+)\s*(?<unit>seconds?|secs?|s|minutes?|mins?|m|hours?|hrs?|h|days?|d|weeks?|wks?|w|months?|mo|years?|y)\s+ago\b", RegexOptions.IgnoreCase)]
    private static partial Regex AgoPattern();

    [GeneratedRegex(@"^\d{1,3}(:\d{2}){1,2}$")]
    private static partial Regex ClockPattern();
}
