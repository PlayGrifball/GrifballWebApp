using GrifballWebApp.Server.Historical.Vods;
using System.Net;
using System.Text;
using System.Text.Json;

namespace GrifballWebApp.Test;

/// <summary>YouTube's answers as the tests serve them: the uploads feed, a channel page, the Data API's lists.</summary>
internal static class YouTubeFixtures
{
    public const string ChannelID = "UCTVJapRl-GGwLCfioTb9N8w";

    public static string FeedEntry(string videoId, string title, string published, string? description = null, bool isShort = false, string channelId = ChannelID) => $"""
          <entry>
            <id>yt:video:{videoId}</id>
            <yt:videoId>{videoId}</yt:videoId>
            <yt:channelId>{channelId}</yt:channelId>
            <title>{WebUtility.HtmlEncode(title)}</title>
            <link rel="alternate" href="https://www.youtube.com/{(isShort ? "shorts/" : "watch?v=")}{videoId}"/>
            <author><name>Grifball</name></author>
            <published>{published}</published>
            <updated>{published}</updated>
            <media:group>
              <media:title>{WebUtility.HtmlEncode(title)}</media:title>
              {(description is null ? "" : $"<media:description>{WebUtility.HtmlEncode(description)}</media:description>")}
            </media:group>
          </entry>
        """;

    public static string Feed(params string[] entries) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <feed xmlns:yt="http://www.youtube.com/xml/schemas/2015" xmlns:media="http://search.yahoo.com/mrss/" xmlns="http://www.w3.org/2005/Atom">
          <link rel="self" href="http://www.youtube.com/feeds/videos.xml?channel_id={ChannelID}"/>
          <id>yt:channel:{ChannelID[2..]}</id>
          <yt:channelId>{ChannelID[2..]}</yt:channelId>
          <title>Grifball</title>
          {string.Join("\n", entries)}
        </feed>
        """;

    /// <summary>A video lockup of today's channel page: "2w ago" and a duration badge.</summary>
    public static object Lockup(string videoId, string title, string ago, string duration) => new
    {
        lockupViewModel = new
        {
            contentImage = new { thumbnailViewModel = new { overlays = new object[] { new { thumbnailBottomOverlayViewModel = new { badges = new[] { new { thumbnailBadgeViewModel = new { text = duration, animatedText = "Now playing" } } } } } } } },
            metadata = new
            {
                lockupMetadataViewModel = new
                {
                    title = new { content = title },
                    metadata = new { contentMetadataViewModel = new { metadataRows = new[] { new { metadataParts = new object[] { new { text = new { content = "43" }, accessibilityLabel = "43 views" }, new { text = new { content = ago }, accessibilityLabel = ago } } } } } },
                },
            },
            contentId = videoId,
            contentType = "LOCKUP_CONTENT_TYPE_VIDEO",
        },
    };

    public static object Playlist(string playlistId, string title) => new
    {
        lockupViewModel = new
        {
            metadata = new { lockupMetadataViewModel = new { title = new { content = title } } },
            contentId = playlistId,
            contentType = "LOCKUP_CONTENT_TYPE_PLAYLIST",
        },
    };

    public static object Short(string videoId) => new
    {
        shortsLockupViewModel = new { onTap = new { innertubeCommand = new { reelWatchEndpoint = new { videoId } } }, overlayMetadata = new { primaryText = new { content = "Flawless Hawk Dive!" } } },
    };

    /// <summary>The older layout's renderer.</summary>
    public static object GridVideo(string videoId, string title, string ago, string length) => new
    {
        gridVideoRenderer = new { videoId, title = new { runs = new[] { new { text = title } } }, publishedTimeText = new { simpleText = ago }, lengthText = new { simpleText = length } },
    };

    public static string ChannelPage(params object[] items)
    {
        var data = new { contents = new { twoColumnBrowseResultsRenderer = new { tabs = new[] { new { tabRenderer = new { content = new { richGridRenderer = new { contents = items } } } } } } } };
        return $"<!DOCTYPE html><html><head><title>Grifball - YouTube</title></head><body><script nonce=\"x\">var ytInitialData = {JsonSerializer.Serialize(data)};</script></body></html>";
    }

    public static string PlaylistItems(string? nextPageToken, params (string VideoId, string Title, string Published)[] items) => JsonSerializer.Serialize(new
    {
        items = items.Select(i => new
        {
            snippet = new { title = i.Title, description = "", publishedAt = i.Published, channelId = ChannelID },
            contentDetails = new { videoId = i.VideoId, videoPublishedAt = i.Published },
        }),
        nextPageToken,
    });

    public static string Videos(params (string VideoId, string Title, string Published, string Duration)[] items) => JsonSerializer.Serialize(new
    {
        items = items.Select(i => new
        {
            id = i.VideoId,
            snippet = new { title = i.Title, description = $"About {i.Title}", publishedAt = i.Published, channelId = ChannelID },
            contentDetails = new { duration = i.Duration },
        }),
    });

    public static HttpResponseMessage Text(string content, string type) => FakeSourcesHandler.Ok(Encoding.UTF8.GetBytes(content), type);
}

/// <summary>Reading YouTube without a key (the uploads feed, a channel page's ytInitialData) and the Data API client.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class YouTubeFeedsTests
{
    private static readonly DateTime FetchedAt = new(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public void ParseFeed_ReadsEachUpload_AndKnowsAShort()
    {
        var xml = YouTubeFixtures.Feed(
            YouTubeFixtures.FeedEntry("Hb2JmxJhQd8", "AGLA Summer Minor Championship | BO7 | Low IQ vs Reach for the Balls", "2026-10-15T23:04:11+00:00", "The final & the trophy"),
            YouTubeFixtures.FeedEntry("dXpxOmv77lw", "Flawless Hawk Dive!", "2026-10-14T10:00:00+00:00", isShort: true),
            "<entry><title>No video ID</title></entry>");

        var videos = YouTubeFeeds.ParseFeed(xml);

        Assert.That(videos, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(videos[0].VideoID, Is.EqualTo("Hb2JmxJhQd8"));
            Assert.That(videos[0].Title, Does.StartWith("AGLA Summer Minor Championship"));
            Assert.That(videos[0].Description, Is.EqualTo("The final & the trophy"));
            Assert.That(videos[0].PublishedAt, Is.EqualTo(new DateTime(2026, 10, 15, 23, 4, 11, DateTimeKind.Utc)));
            Assert.That(videos[0].PublishedAtIsApproximate, Is.False);
            Assert.That(videos[0].ChannelID, Is.EqualTo(YouTubeFixtures.ChannelID));
            Assert.That(videos[0].IsShort, Is.False);
            Assert.That(videos[1].IsShort, Is.True);
            Assert.That(videos[1].Description, Is.Null);
        });
    }

    [Test]
    public void ParseChannelPage_ReadsVideoLockups_AndTheOlderRenderers_ButNotShortsOrPlaylists()
    {
        var html = YouTubeFixtures.ChannelPage(
            YouTubeFixtures.Lockup("Hb2JmxJhQd8", "AGLA Summer Minor Championship", "2w ago", "1:50:01"),
            YouTubeFixtures.Lockup("Hb2JmxJhQd8", "Listed twice", "2w ago", "1:50:01"),
            YouTubeFixtures.Playlist("PL3ulWpN2mnRh2fDXMgNRV4ohrhkyFYfrs", "Late League"),
            YouTubeFixtures.Short("dXpxOmv77lw"),
            YouTubeFixtures.GridVideo("OldVideo001", "SL24 Week 3 - Alpha vs Bravo", "2 years ago", "58:20"));

        var videos = YouTubeFeeds.ParseChannelPage(html, FetchedAt, YouTubeFixtures.ChannelID);

        Assert.That(videos.Select(v => v.VideoID), Is.EquivalentTo(new[] { "Hb2JmxJhQd8", "OldVideo001" }));
        var lockup = videos.Single(v => v.VideoID == "Hb2JmxJhQd8");
        var old = videos.Single(v => v.VideoID == "OldVideo001");
        Assert.Multiple(() =>
        {
            Assert.That(lockup.Title, Is.EqualTo("AGLA Summer Minor Championship"), "The first listing");
            Assert.That(lockup.PublishedAt, Is.EqualTo(FetchedAt.AddDays(-14)));
            Assert.That(lockup.PublishedAtIsApproximate, Is.True);
            Assert.That(lockup.DurationSeconds, Is.EqualTo(6601));
            Assert.That(lockup.ChannelID, Is.EqualTo(YouTubeFixtures.ChannelID));
            Assert.That(old.Title, Is.EqualTo("SL24 Week 3 - Alpha vs Bravo"));
            Assert.That(old.PublishedAt, Is.EqualTo(FetchedAt.AddYears(-2)));
            Assert.That(old.DurationSeconds, Is.EqualTo(3500));
        });
    }

    [TestCase("<html>no data</html>")]
    [TestCase("<script>var ytInitialData = {\"broken\": ;</script>")]
    [TestCase("<script>var ytInitialData = {\"never\": \"closed\"}")]
    [TestCase("<script>var ytInitialData = null</script>")]
    public void ParseChannelPage_GivesNothing_WithoutItsData(string html)
        => Assert.That(YouTubeFeeds.ParseChannelPage(html, FetchedAt, null), Is.Empty);

    [TestCase("30 seconds ago", 0, 0, 0, 30)]
    [TestCase("10 minutes ago", 0, 0, 10, 0)]
    [TestCase("5h ago", 0, 5, 0, 0)]
    [TestCase("Streamed 3 days ago", 3, 0, 0, 0)]
    [TestCase("2w ago", 14, 0, 0, 0)]
    public void Ago_IsThatLongBefore(string text, int days, int hours, int minutes, int seconds)
        => Assert.That(YouTubeFeeds.Ago(text, FetchedAt), Is.EqualTo(FetchedAt - new TimeSpan(days, hours, minutes, seconds)));

    [Test]
    public void Ago_ReadsMonthsAndYears_AndNothingElse()
    {
        Assert.Multiple(() =>
        {
            Assert.That(YouTubeFeeds.Ago("4mo ago", FetchedAt), Is.EqualTo(FetchedAt.AddMonths(-4)));
            Assert.That(YouTubeFeeds.Ago("1y ago", FetchedAt), Is.EqualTo(FetchedAt.AddYears(-1)));
            Assert.That(YouTubeFeeds.Ago("43 views", FetchedAt), Is.Null);
        });
    }

    [Test]
    public void Durations_AreReadInSeconds()
    {
        Assert.Multiple(() =>
        {
            Assert.That(YouTubeFeeds.Clock("1:50:01"), Is.EqualTo(6601));
            Assert.That(YouTubeFeeds.Clock("12:34"), Is.EqualTo(754));
            Assert.That(YouTubeFeeds.Clock("Now playing"), Is.Null);
            Assert.That(YouTubeFeeds.Clock(null), Is.Null);
            Assert.That(YouTubeFeeds.IsoDuration("PT1H50M1S"), Is.EqualTo(6601));
            Assert.That(YouTubeFeeds.IsoDuration("P1DT2H"), Is.EqualTo(93600), "Past a day");
            Assert.That(YouTubeFeeds.IsoDuration("soon"), Is.Null);
            Assert.That(YouTubeFeeds.IsoDuration(" "), Is.Null);
        });
    }

    [Test]
    public void ChannelIdFromUrl_ReadsAChannelLink()
    {
        Assert.Multiple(() =>
        {
            Assert.That(YouTubeFeeds.ChannelIdFromUrl("https://www.youtube.com/channel/UCTVJapRl-GGwLCfioTb9N8w"), Is.EqualTo(YouTubeFixtures.ChannelID));
            Assert.That(YouTubeFeeds.ChannelIdFromUrl("https://www.youtube.com/@grifball"), Is.Null);
            Assert.That(YouTubeFeeds.FeedUrl(YouTubeFixtures.ChannelID), Is.EqualTo("https://www.youtube.com/feeds/videos.xml?channel_id=UCTVJapRl-GGwLCfioTb9N8w"));
        });
    }

    [Test]
    public void Merge_KeepsWhatIsKnownBetter()
    {
        var page = new YouTubeVideo { VideoID = "a", Title = "Title", PublishedAt = FetchedAt.AddDays(-14), PublishedAtIsApproximate = true, DurationSeconds = 600 };
        var feed = new YouTubeVideo { VideoID = "a", Title = "Title (edited)", Description = "Desc", PublishedAt = new DateTime(2026, 10, 15, 23, 4, 11, DateTimeKind.Utc) };
        var later = new YouTubeVideo { VideoID = "a", PublishedAt = FetchedAt, PublishedAtIsApproximate = true, IsShort = true };

        var merged = page.Merge(feed).Merge(later);

        Assert.Multiple(() =>
        {
            Assert.That(merged.Title, Is.EqualTo("Title (edited)"));
            Assert.That(merged.Description, Is.EqualTo("Desc"));
            Assert.That((merged.PublishedAt, merged.PublishedAtIsApproximate), Is.EqualTo(((DateTime?)new DateTime(2026, 10, 15, 23, 4, 11, DateTimeKind.Utc), false)), "An exact date stays");
            Assert.That(merged.DurationSeconds, Is.EqualTo(600));
            Assert.That(merged.IsShort, Is.True);
        });
    }

    // The client, over a fake handler

    private static YouTubeClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond, out FakeSourcesHandler handler)
    {
        handler = new FakeSourcesHandler(respond);
        return new YouTubeClient(new HttpClient(handler));
    }

    [Test]
    public async Task Feed_IsRead_OrFailsWithWhy()
    {
        var ok = Client(_ => YouTubeFixtures.Text(YouTubeFixtures.Feed(YouTubeFixtures.FeedEntry("v1", "One", "2026-10-15T00:00:00Z")), "application/atom+xml"), out _);
        var missing = Client(_ => FakeSourcesHandler.Status(HttpStatusCode.NotFound), out _);
        var html = Client(_ => YouTubeFixtures.Text("<html><body>consent</body>", "text/html"), out _);

        Assert.That((await ok.Feed(YouTubeFixtures.ChannelID, CancellationToken.None)).Single().VideoID, Is.EqualTo("v1"));
        var notFound = await Assert.ThrowsAsync<YouTubeFetchException>(() => missing.Feed(YouTubeFixtures.ChannelID, CancellationToken.None));
        Assert.That(notFound!.Message, Does.Contain("404"));
        await Assert.ThrowsAsync<YouTubeFetchException>(() => html.Feed(YouTubeFixtures.ChannelID, CancellationToken.None));
    }

    [Test]
    public async Task Uploads_ReadsEveryPageOfTheUploadsPlaylist()
    {
        var client = Client(request =>
        {
            var query = request.RequestUri!.Query;
            return YouTubeFixtures.Text(query.Contains("pageToken=P2")
                ? YouTubeFixtures.PlaylistItems(null, ("v2", "Two", "2024-03-02T00:00:00Z"))
                : YouTubeFixtures.PlaylistItems("P2", ("v1", "One", "2024-03-01T00:00:00Z")), "application/json");
        }, out var handler);

        var videos = await client.Uploads(YouTubeFixtures.ChannelID, "test-key", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(videos.Select(v => v.VideoID), Is.EqualTo(new[] { "v1", "v2" }));
            Assert.That(videos[0].PublishedAt, Is.EqualTo(new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(videos[0].Description, Is.Null, "A blank description is none");
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
            Assert.That(handler.Requests[0], Does.Contain("playlistId=UUTVJapRl-GGwLCfioTb9N8w").And.Contain("key=test-key"));
        });
    }

    [Test]
    public async Task Videos_AreAskedFiftyAtATime_WithTheirDurations()
    {
        var ids = Enumerable.Range(0, 51).Select(i => $"vid{i:D8}").ToList();
        var client = Client(request => YouTubeFixtures.Text(request.RequestUri!.Query.Contains("vid00000050")
            ? YouTubeFixtures.Videos(("vid00000050", "Last", "2024-03-01T00:00:00Z", "PT58M20S"))
            : YouTubeFixtures.Videos(("vid00000000", "First", "2024-03-01T00:00:00Z", "PT1H50M1S")), "application/json"), out var handler);

        var videos = await client.Videos(ids, "test-key", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
            Assert.That(videos.Select(v => (v.VideoID, v.DurationSeconds)), Is.EqualTo(new[] { ("vid00000000", (int?)6601), ("vid00000050", (int?)3500) }));
            Assert.That(videos[0].Description, Is.EqualTo("About First"));
        });
    }

    [TestCase(HttpStatusCode.Forbidden, "{}")]
    [TestCase(HttpStatusCode.OK, "not json")]
    [TestCase(HttpStatusCode.OK, "null")]
    public async Task DataApi_FailsWithWhy_WithoutTheKey(HttpStatusCode status, string body)
    {
        var client = Client(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }, out _);

        var error = await Assert.ThrowsAsync<YouTubeFetchException>(() => client.Videos(["v1"], "secret-key", CancellationToken.None));

        Assert.That(error!.Message, Does.Not.Contain("secret-key"));
    }
}
