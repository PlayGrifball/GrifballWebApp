using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrifballWebApp.Application.Historical.Vods;

/// <summary>YouTube:ApiKey: optional. Set, the refresh reads the channel's whole uploads playlist and each video's
/// duration from the YouTube Data API v3; unset, only the feed and the channel page.</summary>
public class YouTubeOptions
{
    public string? ApiKey { get; set; }
}

/// <summary>A YouTube answer that wasn't the content (an error status, a quota error, not XML).</summary>
public sealed class YouTubeFetchException(string message) : Exception(message);

/// <summary>
/// YouTube over HTTP: the channel's uploads feed (no key) and, with a key, the Data API v3 (playlistItems for the uploads,
/// videos for durations and exact dates). Each call is a unit of the key's 10,000 a day; a refresh makes a few dozen.
/// </summary>
public class YouTubeClient
{
    public const string ApiBase = "https://www.googleapis.com/youtube/v3/";
    /// <summary>The most uploads read through the API in one refresh (a page is 50).</summary>
    public const int MaxUploadPages = 40;

    private readonly HttpClient _http;

    public YouTubeClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>The channel's latest uploads (about 15) from its RSS feed.</summary>
    public async Task<List<YouTubeVideo>> Feed(string channelId, CancellationToken ct)
    {
        using var response = await _http.GetAsync(YouTubeFeeds.FeedUrl(channelId), ct);
        if (!response.IsSuccessStatusCode)
            throw new YouTubeFetchException($"the feed answered {(int)response.StatusCode}");
        try
        {
            return YouTubeFeeds.ParseFeed(await response.Content.ReadAsStringAsync(ct));
        }
        catch (System.Xml.XmlException e)
        {
            throw new YouTubeFetchException($"the feed isn't XML: {e.Message}");
        }
    }

    /// <summary>Every upload of the channel (its uploads playlist, "UU" + the channel ID's rest), newest first, without durations.</summary>
    public async Task<List<YouTubeVideo>> Uploads(string channelId, string apiKey, CancellationToken ct)
    {
        var playlistId = "UU" + channelId[2..];
        var videos = new List<YouTubeVideo>();
        string? pageToken = null;
        for (var page = 0; page < MaxUploadPages; page++)
        {
            var url = $"{ApiBase}playlistItems?part=snippet,contentDetails&maxResults=50&playlistId={Uri.EscapeDataString(playlistId)}&key={Uri.EscapeDataString(apiKey)}"
                + (pageToken is null ? "" : $"&pageToken={Uri.EscapeDataString(pageToken)}");
            var list = await Get<ApiList<PlaylistItem>>(url, ct);
            foreach (var item in list.Items ?? [])
            {
                if (item.ContentDetails?.VideoId is not { } id)
                    continue;
                videos.Add(new YouTubeVideo
                {
                    VideoID = id,
                    Title = item.Snippet?.Title,
                    Description = Blank(item.Snippet?.Description),
                    PublishedAt = item.ContentDetails.VideoPublishedAt?.UtcDateTime ?? item.Snippet?.PublishedAt?.UtcDateTime,
                    ChannelID = item.Snippet?.ChannelId ?? channelId,
                });
            }
            pageToken = list.NextPageToken;
            if (pageToken is null)
                break;
        }
        return videos;
    }

    /// <summary>The videos' details (title, description, exact date, duration), 50 to a call. A video that's gone or
    /// private isn't in the answer.</summary>
    public async Task<List<YouTubeVideo>> Videos(IReadOnlyCollection<string> videoIds, string apiKey, CancellationToken ct)
    {
        var videos = new List<YouTubeVideo>();
        foreach (var chunk in videoIds.Chunk(50))
        {
            var url = $"{ApiBase}videos?part=snippet,contentDetails&id={Uri.EscapeDataString(string.Join(',', chunk))}&key={Uri.EscapeDataString(apiKey)}";
            var list = await Get<ApiList<VideoItem>>(url, ct);
            foreach (var item in list.Items ?? [])
            {
                if (item.Id is null)
                    continue;
                videos.Add(new YouTubeVideo
                {
                    VideoID = item.Id,
                    Title = item.Snippet?.Title,
                    Description = Blank(item.Snippet?.Description),
                    PublishedAt = item.Snippet?.PublishedAt?.UtcDateTime,
                    DurationSeconds = YouTubeFeeds.IsoDuration(item.ContentDetails?.Duration),
                    ChannelID = item.Snippet?.ChannelId,
                });
            }
        }
        return videos;
    }

    private async Task<T> Get<T>(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            // Not the URL: it holds the key
            throw new YouTubeFetchException($"the Data API answered {(int)response.StatusCode}");
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct) ?? throw new YouTubeFetchException("the Data API answered nothing");
        }
        catch (JsonException e)
        {
            throw new YouTubeFetchException($"the Data API's answer isn't JSON: {e.Message}");
        }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class ApiList<T>
    {
        public List<T>? Items { get; set; }
        public string? NextPageToken { get; set; }
    }

    private sealed class Snippet
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public string? ChannelId { get; set; }
    }

    private sealed class PlaylistItem
    {
        public Snippet? Snippet { get; set; }
        public PlaylistItemDetails? ContentDetails { get; set; }
    }

    private sealed class PlaylistItemDetails
    {
        public string? VideoId { get; set; }
        public DateTimeOffset? VideoPublishedAt { get; set; }
    }

    private sealed class VideoItem
    {
        public string? Id { get; set; }
        public Snippet? Snippet { get; set; }
        public VideoDetails? ContentDetails { get; set; }
    }

    private sealed class VideoDetails
    {
        [JsonPropertyName("duration")]
        public string? Duration { get; set; }
    }
}
