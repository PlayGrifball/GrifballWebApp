using System.Net;
using System.Text;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// VodService.Sync against the fake context, YouTube answered by a <see cref="FakeSourcesHandler"/>: the channel sources'
/// page snapshots, the uploads feed and (with a key) the Data API, merged one row per video ID; what couldn't be read
/// reported and skipped; the archive's capture for the league's channel. No season is seeded, so every VOD found is left
/// for review (the matcher's own rules are VodMatchingTests'). Also the two refusals of Link that VodServiceUnitTests leaves.
/// </summary>
[TestFixture]
internal sealed class VodServiceSyncUnitTests : ApplicationDbContextTests
{
    /// <summary>Not the league's channel: the archive's capture stays out unless a test asks for it.</summary>
    private const string TestChannel = "UCabcdefghijklmnopqrstuv";
    private const string SourceID = "test-youtube-channel";
    private static readonly DateTime PageFetchedAt = new(2024, 3, 16, 0, 0, 0, DateTimeKind.Utc);

    private FakeSourcesHandler _handler = null!;
    private string? _feed;
    private string? _playlist;
    private string? _videos;
    private HttpStatusCode _apiStatus = HttpStatusCode.OK;
    private bool _feedTimesOut;

    [SetUp]
    public void SetUp()
    {
        _handler = new FakeSourcesHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("/feeds/videos.xml") && _feedTimesOut)
                throw new TaskCanceledException("The feed took too long");
            if (url.Contains("googleapis.com") && _apiStatus != HttpStatusCode.OK)
                return FakeSourcesHandler.Status(_apiStatus);
            var body = url.Contains("/feeds/videos.xml") ? _feed
                : url.Contains("/playlistItems") ? _playlist
                : url.Contains("/youtube/v3/videos") ? _videos
                : null;
            return body is null ? FakeSourcesHandler.Status(HttpStatusCode.NotFound) : YouTubeFixtures.Text(body, url.Contains("googleapis") ? "application/json" : "text/xml");
        });
    }

    private VodService Service(string? apiKey = null, ILogger<VodService>? logger = null)
        => new(Context, new YouTubeClient(new HttpClient(_handler)), Options.Create(new YouTubeOptions { ApiKey = apiKey }), logger ?? NullLogger<VodService>.Instance);

    private async Task<HistoricalSource> AddChannel(string url, string id = SourceID)
    {
        var source = HistoricalSource.FromCatalog(id, new HistoricalSourceListing { Category = "VODs", Title = "Channel", Url = url, Kind = HistoricalSourceKind.YouTubeChannel }, enabled: true);
        Context.HistoricalSources.Add(source);
        await Context.SaveChangesAsync();
        return source;
    }

    private Task<HistoricalSource> AddChannel() => AddChannel($"https://www.youtube.com/channel/{TestChannel}");

    private async Task AddSnapshot(HistoricalSource source, string html)
    {
        source.Fetched(PageFetchedAt, Encoding.UTF8.GetBytes(html), "text/html", null);
        await Context.SaveChangesAsync();
    }

    private Task<Vod> Row(string videoId) => Context.Vods.AsNoTracking().SingleAsync(v => v.YouTubeVideoID == videoId);

    /// <summary>The page lists the VOD (approximately dated, with its length) and a clip; the feed has the VOD exactly, a
    /// chat and a Short.</summary>
    private async Task ChannelWithVideos()
    {
        var source = await AddChannel();
        await AddSnapshot(source, YouTubeFixtures.ChannelPage(
            YouTubeFixtures.Lockup("week1video0", "SL24 Week 1 - Alpha vs Bravo", "2w ago", "1:50:01"),
            YouTubeFixtures.Lockup("clipvideo00", "SL24 Week 1 - Alpha vs Bravo highlight", "2w ago", "0:45")));
        _feed = YouTubeFixtures.Feed(
            YouTubeFixtures.FeedEntry("week1video0", "SL24 Week 1 - Alpha vs Bravo", "2024-03-02T20:00:00+00:00", "Cast by Rey", channelId: TestChannel),
            YouTubeFixtures.FeedEntry("chatvideo00", "Fireside Chat with the Commish", "2024-03-20T18:00:00+00:00", channelId: TestChannel),
            YouTubeFixtures.FeedEntry("shortvideo0", "Flawless Hawk Dive!", "2024-03-21T18:00:00+00:00", isShort: true, channelId: TestChannel));
    }

    private static readonly string[] Week1AndChat = ["chatvideo00", "week1video0"];
    private static readonly string[] Week1Only = ["week1video0"];
    private static readonly string[] FindingTheTestChannel = [$"Finding the VODs of {SourceID}"];
    private static readonly string[] FeedNotFound = [$"{SourceID} feed: the feed answered 404"];
    private static readonly string[] FeedTimedOut = [$"{SourceID} feed: timed out"];
    private static readonly string[] BrokenChannelFailed = ["broken-channel: no channel ID in https://www.youtube.com/@grifball"];
    private static readonly string[] ApiForbidden = [$"{SourceID} uploads: the Data API answered 403", "YouTube videos: the Data API answered 403"];

    [Test]
    public async Task Sync_LogsWhatTheRefreshFoundAndDid()
    {
        await ChannelWithVideos();
        var logger = new FakeLogger<VodService>();

        await Service(logger: logger).Sync(_ => Task.CompletedTask);

        // Found: the VOD, the clip, the chat and the Short; added: the VOD and the chat, both left for review
        Assert.That(logger.Collector.GetSnapshot().Select(r => (r.Level, r.Message)), Has.Member((LogLevel.Information,
            "VODs refreshed: 4 found, 2 added, 0 updated, 0 linked, 2 need review, 0 failed")));
    }

    [Test]
    public async Task Sync_AddsEachVideoOnce_FromThePageAndTheFeed_ButNoClipOrShort()
    {
        await ChannelWithVideos();
        var progress = new List<string>();

        var result = await Service().Sync(message =>
        {
            progress.Add(message);
            return Task.CompletedTask;
        });

        var week1 = await Row("week1video0");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.Vods.OrderBy(v => v.YouTubeVideoID).Select(v => v.YouTubeVideoID).ToListAsync(), Is.EqualTo(Week1AndChat));
            Assert.That((result.Added, result.Updated, result.Linked, result.NeedsReview), Is.EqualTo((2, 0, 0, 2)), "No season to link them to: both left for review");
            Assert.That(result.Failed, Is.Empty);
            Assert.That(progress, Is.EqualTo(FindingTheTestChannel));
            Assert.That((week1.PublishedAt, week1.PublishedAtIsApproximate), Is.EqualTo((new DateTime(2024, 3, 2, 20, 0, 0, DateTimeKind.Utc), false)), "The feed's exact date over the page's 2w ago");
            Assert.That(week1.DurationSeconds, Is.EqualTo(6601), "The page's length");
            Assert.That(week1.Description, Is.EqualTo("Cast by Rey"));
            Assert.That(week1.ChannelID, Is.EqualTo(TestChannel));
            Assert.That(week1.MatchStatus, Is.EqualTo(VodMatchStatus.NeedsReview));
        }
    }

    [Test]
    public async Task Sync_Again_AddsNothing_AndOnlyCompletesWhatALaterSourceKnows()
    {
        var source = await AddChannel();
        await AddSnapshot(source, YouTubeFixtures.ChannelPage(YouTubeFixtures.Lockup("week1video0", "SL24 Week 1 - Alpha vs Bravo", "2w ago", "1:50:01")));
        var first = await Service().Sync();
        var approximate = await Row("week1video0");

        _feed = YouTubeFixtures.Feed(YouTubeFixtures.FeedEntry("week1video0", "SL24 Week 1 - Alpha vs Bravo (full cast)", "2024-03-02T20:00:00+00:00", "Cast by Rey", channelId: TestChannel));
        var second = await Service().Sync();
        var third = await Service().Sync();

        var exact = await Row("week1video0");
        using (Assert.EnterMultipleScope())
        {
            Assert.That((first.Added, second.Added, second.Updated, third.Added, third.Updated), Is.EqualTo((1, 0, 1, 0, 0)));
            Assert.That(first.Failed, Is.EqualTo(FeedNotFound));
            Assert.That(await Context.Vods.CountAsync(), Is.EqualTo(1), "One row per video ID");
            Assert.That((approximate.PublishedAt, approximate.PublishedAtIsApproximate), Is.EqualTo((new DateTime(2024, 3, 2, 0, 0, 0, DateTimeKind.Utc), true)));
            Assert.That((exact.PublishedAt, exact.PublishedAtIsApproximate), Is.EqualTo((new DateTime(2024, 3, 2, 20, 0, 0, DateTimeKind.Utc), false)));
            Assert.That(exact.Title, Is.EqualTo("SL24 Week 1 - Alpha vs Bravo (full cast)"));
            Assert.That(exact.Description, Is.EqualTo("Cast by Rey"));
        }
    }

    [Test]
    public async Task Sync_ReportsAChannelWithoutAnID_AndKeepsTheOthers()
    {
        await ChannelWithVideos();
        await AddChannel("https://www.youtube.com/@grifball", "broken-channel");

        var result = await Service().Sync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Failed, Is.EqualTo(BrokenChannelFailed));
            Assert.That(result.Added, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Sync_AFeedThatTimesOut_IsReportedAsSuch_AndThePageStillCounts()
    {
        await ChannelWithVideos();
        _feedTimesOut = true;

        var result = await Service().Sync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Failed, Is.EqualTo(FeedTimedOut));
            Assert.That(await Context.Vods.Select(v => v.YouTubeVideoID).ToListAsync(), Is.EqualTo(Week1Only), "The page's VOD, still approximately dated");
            Assert.That((await Row("week1video0")).PublishedAtIsApproximate, Is.True);
        }
    }

    [Test]
    public async Task Sync_WithAnApiKey_ReadsEveryUpload_AndTheirLengths_AndNeverAsksAgainAboutACompleteOne()
    {
        await AddChannel();
        _playlist = YouTubeFixtures.PlaylistItems(null, ("week1video0", "SL24 Week 1 - Alpha vs Bravo", "2024-03-02T20:00:00Z"), ("clipvideo00", "Highlight", "2024-03-03T20:00:00Z"));
        _videos = YouTubeFixtures.Videos(("week1video0", "SL24 Week 1 - Alpha vs Bravo", "2024-03-02T20:00:00Z", "PT1H50M1S"), ("clipvideo00", "Highlight", "2024-03-03T20:00:00Z", "PT2M"));

        var result = await Service(" test-key ").Sync();

        var week1 = await Row("week1video0");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.Vods.Select(v => v.YouTubeVideoID).ToListAsync(), Is.EqualTo(Week1Only), "Two minutes is a clip");
            Assert.That((week1.DurationSeconds, week1.PublishedAtIsApproximate), Is.EqualTo(((int?)6601, false)));
            Assert.That(week1.Description, Is.EqualTo("About SL24 Week 1 - Alpha vs Bravo"));
            Assert.That(_handler.Requests, Has.Some.Matches<string>(r => r is not null && r.Contains("/playlistItems?") && r.Contains("key=test-key")), "The key, trimmed");
            Assert.That(_handler.Requests, Has.Some.Matches<string>(r => r is not null && r.Contains("/videos?") && r.Contains("week1video0")));
            Assert.That(result.Failed, Is.EqualTo(FeedNotFound), "No feed; the API's videos still count");
        }

        _handler.Requests.Clear();
        await Service("test-key").Sync();
        Assert.That(_handler.Requests.Where(r => r.Contains("/videos?")).ToList(), Has.All.Not.Contains("week1video0"));
    }

    [Test]
    public async Task Sync_WithAnApiKey_ThatFails_IsReported_AndThePageAndFeedStillCount()
    {
        await ChannelWithVideos();
        _apiStatus = HttpStatusCode.Forbidden;

        var result = await Service("test-key").Sync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Failed, Is.EquivalentTo(ApiForbidden));
            Assert.That(result.Added, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Sync_WithAnApiKey_AndNothingFound_AsksTheDataApiNothing()
    {
        var result = await Service("test-key").Sync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_handler.Requests, Is.Empty);
            Assert.That((result.Added, result.Updated, result.Linked, result.NeedsReview, result.Failed.Count), Is.EqualTo((0, 0, 0, 0, 0)));
        }
    }

    [Test]
    public async Task Sync_TheLeaguesChannel_BackfillsTheArchivesCapture()
    {
        await AddChannel($"https://www.youtube.com/channel/{VodArchive.ChannelID}", "youtube-grifball-channel");

        var result = await Service().Sync();

        var rows = await Context.Vods.AsNoTracking().ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Added, Is.EqualTo(VodArchive.Videos.Count));
            Assert.That(rows.Select(v => v.YouTubeVideoID), Is.EquivalentTo(VodArchive.Videos.Select(v => v.VideoID)));
            Assert.That(rows.Select(v => v.PublishedAtIsApproximate), Is.All.True, "The capture's dates are its '2w ago'");
            Assert.That(rows.Single(v => v.YouTubeVideoID == "dq3DOSH23z4").DurationSeconds, Is.EqualTo(14597));
            Assert.That(rows.Select(v => v.ChannelID), Is.All.EqualTo(VodArchive.ChannelID));
        }
    }

    [Test]
    public async Task Sync_TheLeaguesChannel_WithAnApiKey_AsksAboutEveryIdTheArchiveFound()
    {
        await AddChannel($"https://www.youtube.com/channel/{VodArchive.ChannelID}", "youtube-grifball-channel");
        // One of the IDs the archive found without a title, which only the API can name
        _videos = YouTubeFixtures.Videos(("8IjYNH7Vsso", "WL23 Grand Final - Alpha vs Bravo", "2023-03-01T00:00:00Z", "PT1H"));

        await Service("test-key").Sync();

        var ids = await Context.Vods.Select(v => v.YouTubeVideoID).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ids, Is.SupersetOf(VodArchive.Videos.Select(v => v.VideoID)));
            Assert.That(ids, Does.Contain("8IjYNH7Vsso"));
            Assert.That(ids, Has.Count.EqualTo(VodArchive.Videos.Count + 1), "The untitled IDs the API doesn't answer for aren't kept");
            Assert.That(_handler.Requests, Has.Some.Matches<string>(r => r is not null && r.Contains("/videos?") && VodArchive.VideoIDs.All(r.Contains)), "Every archive ID asked about");
        }
    }

    // ---------- Link: a season match that doesn't fit ----------

    [Test]
    public async Task Link_NoSuchSeasonMatch_ReturnsMessage()
    {
        Context.Vods.Add(Vod.Seed("v1", "Title", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), VodMatchStatus.NeedsReview));
        await Context.SaveChangesAsync();
        var vod = await Context.Vods.SingleAsync();

        var result = await Service().Link(vod.VodID, new LinkVodRequest { SeasonMatchID = 999 });

        Assert.That(result, Is.EqualTo("There is no such season match."));
    }

    [Test]
    public async Task Link_ASeasonMatchOfAnotherSeason_ReturnsMessage_AndLinksNothing()
    {
        Context.Seasons.AddRange(Season.Seed(seasonName: "S1", seasonID: 1), Season.Seed(seasonName: "S2", seasonID: 2));
        Context.Teams.AddRange(Team.Seed(teamName: "Home", seasonID: 1, teamID: 10), Team.Seed(teamName: "Away", seasonID: 1, teamID: 20));
        var match = SeasonMatch.Between(1, 10, 20, bestOf: 1);
        Context.SeasonMatches.Add(match);
        Context.Vods.Add(Vod.Seed("v1", "Title", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), VodMatchStatus.NeedsReview));
        await Context.SaveChangesAsync();
        var vod = await Context.Vods.SingleAsync();

        var result = await Service().Link(vod.VodID, new LinkVodRequest { SeasonID = 2, SeasonMatchID = match.SeasonMatchID });

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo("The season match isn't in that season."));
            Assert.That((await Context.Vods.SingleAsync()).MatchStatus, Is.EqualTo(VodMatchStatus.NeedsReview));
        }
    }
}
