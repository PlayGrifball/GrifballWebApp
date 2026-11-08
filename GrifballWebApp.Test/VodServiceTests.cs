using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Historical.Sources;
using GrifballWebApp.Server.Historical.Vods;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using System.Text;

namespace GrifballWebApp.Test;

/// <summary>
/// The VODs end to end against the database: found from the channel page snapshot, the feed and the Data API (a fake
/// handler answers), one row per video ID, matched, reviewed (link, dismiss, both remembered), shown on the pages, and
/// run by the historical refresh and import jobs.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class VodServiceTests
{
    /// <summary>Not the league's channel: the archive's capture stays out of these tests unless one asks for it.</summary>
    private const string TestChannel = "UCabcdefghijklmnopqrstuv";
    private const string SourceID = "test-youtube-channel";

    private GrifballContext _context = null!;
    private FakeSourcesHandler _handler = null!;
    private string? _feed;
    private string? _page;
    private string? _playlist;
    private string? _videos;
    private HttpStatusCode _apiStatus = HttpStatusCode.OK;
    private Season _season = null!;
    private Team _alpha = null!;
    private Team _bravo = null!;
    private SeasonMatch _week1 = null!;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _handler = new FakeSourcesHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            string? body = url.Contains("/feeds/videos.xml") ? _feed
                : url.Contains("/playlistItems") ? _playlist
                : url.Contains("/youtube/v3/videos") ? _videos
                : url.Contains("youtube.com/channel/") ? _page
                : null;
            if (url.Contains("googleapis.com") && _apiStatus != HttpStatusCode.OK)
                return FakeSourcesHandler.Status(_apiStatus);
            return body is null ? FakeSourcesHandler.Status(HttpStatusCode.NotFound) : YouTubeFixtures.Text(body, url.Contains("googleapis") ? "application/json" : "text/xml");
        });

        _season = new Season { SeasonName = "Spring League 2024" }.WithDates(seasonStart: new DateTime(2024, 3, 1), seasonEnd: new DateTime(2024, 5, 31));
        _context.Seasons.Add(_season);
        await _context.SaveChangesAsync();
        _alpha = new Team { SeasonID = _season.SeasonID, TeamName = "Alpha" };
        _bravo = new Team { SeasonID = _season.SeasonID, TeamName = "Bravo" };
        _context.Teams.AddRange(_alpha, _bravo);
        await _context.SaveChangesAsync();
        _week1 = SeasonMatch.Seed(_season.SeasonID, _alpha.TeamID, _bravo.TeamID, bestOf: 3, scheduledTime: new DateTime(2024, 3, 1, 20, 0, 0));
        _context.SeasonMatches.Add(_week1);
        await _context.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private VodService Service(string? apiKey = null, HttpMessageHandler? handler = null)
        => new(_context, new YouTubeClient(new HttpClient(handler ?? _handler)), Options.Create(new YouTubeOptions { ApiKey = apiKey }), NullLogger<VodService>.Instance);

    private async Task AddChannel(string channelId = TestChannel, string id = SourceID)
    {
        _context.HistoricalSources.Add(HistoricalTestData.Source(id, $"https://www.youtube.com/channel/{channelId}", kind: HistoricalSourceKind.YouTubeChannel, category: "VODs", title: "Channel"));
        await _context.SaveChangesAsync();
    }

    private async Task AddSnapshot(string html, DateTime fetchedAt, string id = SourceID)
    {
        var content = Encoding.UTF8.GetBytes(html);
        var source = await _context.HistoricalSources.SingleAsync(s => s.HistoricalSourceID == id);
        source.Fetched(fetchedAt, content, "text/html", null);
        await _context.SaveChangesAsync();
    }

    private Task<Vod> Row(string videoId) => _context.Vods.AsNoTracking().Include(v => v.Teams).SingleAsync(v => v.YouTubeVideoID == videoId);

    /// <summary>The page (the week 1 VOD, a clip) and the feed (the same VOD exactly, a chat, a Short).</summary>
    private async Task ChannelWithVideos()
    {
        await AddChannel();
        await AddSnapshot(YouTubeFixtures.ChannelPage(
            YouTubeFixtures.Lockup("week1video0", "SL24 Week 1 - Alpha vs Bravo", "2w ago", "1:50:01"),
            YouTubeFixtures.Lockup("clipvideo00", "SL24 Week 1 - Alpha vs Bravo highlight", "2w ago", "0:45")), new DateTime(2024, 3, 16));
        _feed = YouTubeFixtures.Feed(
            YouTubeFixtures.FeedEntry("week1video0", "SL24 Week 1 - Alpha vs Bravo", "2024-03-02T20:00:00+00:00", "Cast by Rey", channelId: TestChannel),
            YouTubeFixtures.FeedEntry("chatvideo00", "Fireside Chat with the Commish", "2024-03-20T18:00:00+00:00", channelId: TestChannel),
            YouTubeFixtures.FeedEntry("shortvideo0", "Flawless Hawk Dive!", "2024-03-21T18:00:00+00:00", isShort: true, channelId: TestChannel));
    }

    [Test]
    public async Task Sync_AddsEachVideoOnce_FromThePageAndTheFeed_AndMatchesThem()
    {
        await ChannelWithVideos();

        var result = await Service().Sync();

        var week1 = await Row("week1video0");
        var chat = await Row("chatvideo00");
        Assert.Multiple(async () =>
        {
            Assert.That(await _context.Vods.Select(v => v.YouTubeVideoID).ToListAsync(), Is.EquivalentTo(new[] { "week1video0", "chatvideo00" }), "No clip, no Short");
            Assert.That((result.Added, result.Linked, result.NeedsReview, result.Failed.Count), Is.EqualTo((2, 1, 1, 0)));
            Assert.That((week1.PublishedAt, week1.PublishedAtIsApproximate), Is.EqualTo((new DateTime(2024, 3, 2, 20, 0, 0), false)), "The feed's exact date over the page's 2w ago");
            Assert.That(week1.DurationSeconds, Is.EqualTo(6601), "The page's duration");
            Assert.That(week1.Description, Is.EqualTo("Cast by Rey"));
            Assert.That(week1.ChannelID, Is.EqualTo(TestChannel));
            Assert.That((week1.MatchStatus, week1.SeasonID, week1.SeasonMatchID), Is.EqualTo((VodMatchStatus.Auto, (int?)_season.SeasonID, (int?)_week1.SeasonMatchID)));
            Assert.That(week1.Teams.Select(t => t.TeamID), Is.EquivalentTo(new[] { _alpha.TeamID, _bravo.TeamID }));
            Assert.That((chat.MatchStatus, chat.SeasonID), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)_season.SeasonID)), "Suggested: running then, no teams");
        });
    }

    [Test]
    public async Task Sync_Again_AddsNothing_AndOnlyCompletesWhatALaterSourceKnows()
    {
        await AddChannel();
        await AddSnapshot(YouTubeFixtures.ChannelPage(YouTubeFixtures.Lockup("week1video0", "SL24 Week 1 - Alpha vs Bravo", "2w ago", "1:50:01")), new DateTime(2024, 3, 16));
        var first = await Service().Sync();
        var approximate = await Row("week1video0");

        _feed = YouTubeFixtures.Feed(YouTubeFixtures.FeedEntry("week1video0", "SL24 Week 1 - Alpha vs Bravo (full cast)", "2024-03-02T20:00:00+00:00", "Cast by Rey", channelId: TestChannel));
        var second = await Service().Sync();
        var third = await Service().Sync();

        var exact = await Row("week1video0");
        Assert.Multiple(async () =>
        {
            Assert.That((first.Added, second.Added, second.Updated, third.Added, third.Updated), Is.EqualTo((1, 0, 1, 0, 0)));
            Assert.That(await _context.Vods.CountAsync(), Is.EqualTo(1), "One row per video ID");
            Assert.That((approximate.PublishedAt, approximate.PublishedAtIsApproximate), Is.EqualTo((new DateTime(2024, 3, 2), true)));
            Assert.That((exact.PublishedAt, exact.PublishedAtIsApproximate), Is.EqualTo((new DateTime(2024, 3, 2, 20, 0, 0), false)));
            Assert.That(exact.Title, Is.EqualTo("SL24 Week 1 - Alpha vs Bravo (full cast)"));
            Assert.That(exact.Description, Is.EqualTo("Cast by Rey"));
        });
    }

    [Test]
    public async Task Sync_ReportsWhatCouldntBeRead_AndKeepsTheRest()
    {
        await ChannelWithVideos();
        _feed = null;
        await AddChannel("not-a-channel-id", "broken-channel");

        var result = await Service().Sync();

        Assert.Multiple(async () =>
        {
            Assert.That(result.Failed, Has.Some.Contains($"{SourceID} feed: the feed answered 404"));
            Assert.That(result.Failed, Has.Some.Contains("broken-channel: no channel ID"));
            Assert.That(await _context.Vods.Select(v => v.YouTubeVideoID).ToListAsync(), Is.EqualTo(new[] { "week1video0" }), "The page's VOD still");
        });
    }

    [Test]
    public async Task Sync_WithAnApiKey_ReadsEveryUpload_AndTheirDurations()
    {
        await AddChannel();
        _playlist = YouTubeFixtures.PlaylistItems(null, ("week1video0", "SL24 Week 1 - Alpha vs Bravo", "2024-03-02T20:00:00Z"), ("clipvideo00", "Highlight", "2024-03-03T20:00:00Z"));
        _videos = YouTubeFixtures.Videos(("week1video0", "SL24 Week 1 - Alpha vs Bravo", "2024-03-02T20:00:00Z", "PT1H50M1S"), ("clipvideo00", "Highlight", "2024-03-03T20:00:00Z", "PT2M"));

        var result = await Service("test-key").Sync();

        var week1 = await Row("week1video0");
        Assert.Multiple(async () =>
        {
            Assert.That(await _context.Vods.Select(v => v.YouTubeVideoID).ToListAsync(), Is.EqualTo(new[] { "week1video0" }), "Two minutes is a clip");
            Assert.That((week1.DurationSeconds, week1.PublishedAtIsApproximate, week1.MatchStatus), Is.EqualTo(((int?)6601, false, VodMatchStatus.Auto)));
            Assert.That(_handler.Requests, Has.Some.Matches<string>(r => r.Contains("/playlistItems?") && r.Contains("key=test-key")));
            Assert.That(_handler.Requests, Has.Some.Matches<string>(r => r.Contains("/videos?") && r.Contains("week1video0")));
            Assert.That(result.Failed, Has.Some.Contains("feed"), "No feed answered; the API's videos still count");
        });

        // Complete now: not asked about again
        _handler.Requests.Clear();
        await Service("test-key").Sync();
        Assert.That(_handler.Requests.Where(r => r.Contains("/videos?")).ToList(), Has.All.Not.Contains("week1video0"));
    }

    [Test]
    public async Task Sync_WithAnApiKey_ThatFails_IsReported()
    {
        await ChannelWithVideos();
        _apiStatus = HttpStatusCode.Forbidden;

        var result = await Service("test-key").Sync();

        Assert.Multiple(async () =>
        {
            Assert.That(result.Failed, Has.Some.Contains("uploads: the Data API answered 403"));
            Assert.That(result.Failed, Has.Some.Contains("YouTube videos: the Data API answered 403"));
            Assert.That(await _context.Vods.CountAsync(), Is.EqualTo(2), "The page and the feed still count");
        });
    }

    [Test]
    public async Task Sync_BackfillsTheArchivesCapture_ForTheLeaguesChannel()
    {
        await AddChannel(VodArchive.ChannelID, "youtube-grifball-channel");
        // One of the IDs the archive found without a title, which only the API can name
        _videos = YouTubeFixtures.Videos(("8IjYNH7Vsso", "WL23 Grand Final - Alpha vs Bravo", "2023-03-01T00:00:00Z", "PT1H"));

        await Service("test-key").Sync();

        var ids = await _context.Vods.Select(v => v.YouTubeVideoID).ToListAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(ids, Is.SupersetOf(VodArchive.Videos.Select(v => v.VideoID)));
            Assert.That(ids, Does.Contain("8IjYNH7Vsso"));
            Assert.That(ids, Has.Count.EqualTo(VodArchive.Videos.Count + 1), "The Shorts' IDs aren't kept");
            Assert.That(_handler.Requests, Has.Some.Matches<string>(r => r.Contains("/videos?") && r.Contains("v327Rjqsij8")), "Every archive ID asked about");
            Assert.That((await Row("Hb2JmxJhQd8")).PublishedAtIsApproximate, Is.True);
        });
    }

    [Test]
    public async Task Review_LinkAndDismiss_AreRemembered()
    {
        await ChannelWithVideos();
        var service = Service();
        await service.Sync();
        var chat = await Row("chatvideo00");
        _context.ChangeTracker.Clear();

        var review = await service.Review();
        Assert.That(review.Select(r => (r.YouTubeVideoID, r.MatchStatus, r.SeasonName)), Is.EqualTo(new[] { ("chatvideo00", "NeedsReview", "Spring League 2024") }));
        Assert.That(review[0].MatchNote, Does.Contain("No teams"));

        Assert.That(await service.Link(chat.VodID, new LinkVodRequest { SeasonID = _season.SeasonID, TeamIDs = [_alpha.TeamID] }), Is.Null);
        var week1 = await Row("week1video0");
        Assert.That(await service.Dismiss(week1.VodID), Is.True);
        _context.ChangeTracker.Clear();

        // A later refresh leaves both decisions alone
        await service.Sync();

        var linked = await Row("chatvideo00");
        var dismissed = await Row("week1video0");
        Assert.Multiple(async () =>
        {
            Assert.That((linked.MatchStatus, linked.SeasonID), Is.EqualTo((VodMatchStatus.Confirmed, (int?)_season.SeasonID)));
            Assert.That(linked.Teams.Select(t => t.TeamID), Is.EqualTo(new[] { _alpha.TeamID }));
            Assert.That(dismissed.MatchStatus, Is.EqualTo(VodMatchStatus.Dismissed), "Never suggested again");
            Assert.That(await service.Review(), Is.Empty);
            Assert.That(await service.ForSeason(_season.SeasonID), Has.Count.EqualTo(1), "The dismissed VOD is hidden");
            Assert.That(await service.Dismiss(-1), Is.False);
        });
    }

    [Test]
    public async Task Link_ChecksWhatItIsGiven()
    {
        var other = new Season { SeasonName = "Spring League 2025" }.WithDates(seasonStart: new DateTime(2025, 3, 1), seasonEnd: new DateTime(2025, 5, 31));
        _context.Seasons.Add(other);
        var vod = new Vod { YouTubeVideoID = "somevideo00", Title = "Something", PublishedAt = new DateTime(2024, 3, 2), ChannelID = TestChannel, MatchStatus = VodMatchStatus.NeedsReview };
        _context.Vods.Add(vod);
        await _context.SaveChangesAsync();
        var otherTeam = new Team { SeasonID = other.SeasonID, TeamName = "Charlie" };
        _context.Teams.Add(otherTeam);
        await _context.SaveChangesAsync();
        var service = Service();

        Assert.Multiple(async () =>
        {
            Assert.That(await service.Link(vod.VodID, new LinkVodRequest()), Is.EqualTo("Pick a season or a season match."));
            Assert.That(await service.Link(vod.VodID, new LinkVodRequest { SeasonID = -1 }), Is.EqualTo("There is no such season."));
            Assert.That(await service.Link(vod.VodID, new LinkVodRequest { SeasonMatchID = -1 }), Is.EqualTo("There is no such season match."));
            Assert.That(await service.Link(vod.VodID, new LinkVodRequest { SeasonID = other.SeasonID, SeasonMatchID = _week1.SeasonMatchID }), Is.EqualTo("The season match isn't in that season."));
            Assert.That(await service.Link(vod.VodID, new LinkVodRequest { SeasonID = _season.SeasonID, TeamIDs = [otherTeam.TeamID] }), Is.EqualTo("Every team must be one of the season's."));
        });
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.Link(-1, new LinkVodRequest { SeasonID = _season.SeasonID }));

        // A match alone: its season and teams follow
        Assert.That(await service.Link(vod.VodID, new LinkVodRequest { SeasonMatchID = _week1.SeasonMatchID }), Is.Null);
        var linked = await Row("somevideo00");
        Assert.Multiple(() =>
        {
            Assert.That((linked.MatchStatus, linked.SeasonID, linked.SeasonMatchID), Is.EqualTo((VodMatchStatus.Confirmed, (int?)_season.SeasonID, (int?)_week1.SeasonMatchID)));
            Assert.That(linked.Teams.Select(t => t.TeamID), Is.EquivalentTo(new[] { _alpha.TeamID, _bravo.TeamID }));
        });
    }

    [Test]
    public async Task Pages_ShowLinkedVods_Only()
    {
        var player = await _context.AddUser("player", gamertag: "Player");
        _context.TeamPlayers.Add(new TeamPlayer { TeamID = _alpha.TeamID, UserID = player.Id });
        var linked = new Vod { YouTubeVideoID = "linkedvideo", Title = "SL24 Week 1 - Alpha vs Bravo", PublishedAt = new DateTime(2024, 3, 2), ChannelID = TestChannel, MatchStatus = VodMatchStatus.Auto, SeasonID = _season.SeasonID, SeasonMatchID = _week1.SeasonMatchID, DurationSeconds = 600 };
        linked.Teams.Add(new VodTeam { TeamID = _alpha.TeamID });
        linked.Teams.Add(new VodTeam { TeamID = _bravo.TeamID });
        var confirmed = new Vod { YouTubeVideoID = "confirmedvd", Title = "Bravo recap", PublishedAt = new DateTime(2024, 3, 10), ChannelID = TestChannel, MatchStatus = VodMatchStatus.Confirmed, SeasonID = _season.SeasonID };
        confirmed.Teams.Add(new VodTeam { TeamID = _bravo.TeamID });
        var unsure = new Vod { YouTubeVideoID = "unsurevideo", Title = "Alpha vs ?", PublishedAt = new DateTime(2024, 3, 11), ChannelID = TestChannel, MatchStatus = VodMatchStatus.NeedsReview, SeasonID = _season.SeasonID, SeasonMatchID = _week1.SeasonMatchID };
        unsure.Teams.Add(new VodTeam { TeamID = _alpha.TeamID });
        _context.Vods.AddRange(linked, confirmed, unsure);
        await _context.SaveChangesAsync();
        var service = Service();

        var match = await service.ForSeasonMatch(_week1.SeasonMatchID);
        var season = await service.ForSeason(_season.SeasonID);
        var alpha = await service.ForTeam(_alpha.TeamID);
        var bravo = await service.ForTeam(_bravo.TeamID);
        var profile = await service.ForPlayer(player.Id);

        Assert.Multiple(() =>
        {
            Assert.That(match.Select(v => v.YouTubeVideoID), Is.EqualTo(new[] { "linkedvideo" }));
            Assert.That(match[0].Teams.Select(t => t.TeamName), Is.EqualTo(new[] { "Alpha", "Bravo" }));
            Assert.That((match[0].SeasonName, match[0].DurationSeconds), Is.EqualTo(("Spring League 2024", (int?)600)));
            Assert.That(season.Select(v => v.YouTubeVideoID), Is.EqualTo(new[] { "confirmedvd", "linkedvideo" }), "Newest first");
            Assert.That(alpha.Select(v => v.YouTubeVideoID), Is.EqualTo(new[] { "linkedvideo" }));
            Assert.That(bravo.Select(v => v.YouTubeVideoID), Is.EqualTo(new[] { "confirmedvd", "linkedvideo" }));
            Assert.That(profile.Select(v => v.YouTubeVideoID), Is.EqualTo(new[] { "linkedvideo" }), "Through the player's team");
        });
    }

    [Test]
    public async Task LinkOptions_AreTheSeasonsTeamsAndMatches()
    {
        var service = Service();

        var seasons = await service.LinkSeasons();
        var options = await service.LinkOptions(_season.SeasonID);

        Assert.Multiple(async () =>
        {
            Assert.That(seasons.Select(s => s.SeasonName), Does.Contain("Spring League 2024"));
            Assert.That(options!.Teams.Select(t => t.TeamName), Is.EqualTo(new[] { "Alpha", "Bravo" }));
            Assert.That(options.Matches.Single().Label, Is.EqualTo("Alpha vs Bravo · Mar 1, 2024"));
            Assert.That(options.Matches.Single().TeamIDs, Is.EqualTo(new[] { _alpha.TeamID, _bravo.TeamID }));
            Assert.That(await service.LinkOptions(-1), Is.Null);
        });
    }

    [Test]
    public async Task Rematch_LinksAVod_OnceItsSeasonIsImported()
    {
        _context.Vods.Add(new Vod { YouTubeVideoID = "latervideo0", Title = "WL23 - Echo vs Foxtrot", PublishedAt = new DateTime(2023, 2, 1), ChannelID = TestChannel, MatchStatus = VodMatchStatus.NeedsReview });
        await _context.SaveChangesAsync();
        var service = Service();
        var before = await service.Rematch();

        var winter = new Season { SeasonName = "Winter League 2023 (DA)", HistoricalSourceID = "wb/wl23da" }.WithDates(seasonStart: new DateTime(2023, 1, 1), seasonEnd: new DateTime(2023, 12, 31));
        _context.Seasons.Add(winter);
        await _context.SaveChangesAsync();
        _context.Teams.AddRange(new Team { SeasonID = winter.SeasonID, TeamName = "Echo" }, new Team { SeasonID = winter.SeasonID, TeamName = "Foxtrot" });
        await _context.SaveChangesAsync();
        var after = await service.Rematch();

        var vod = await Row("latervideo0");
        Assert.Multiple(() =>
        {
            Assert.That(before, Is.EqualTo((0, 1)));
            Assert.That(after, Is.EqualTo((1, 0)));
            Assert.That((vod.MatchStatus, vod.SeasonID, vod.Teams.Count), Is.EqualTo((VodMatchStatus.Auto, (int?)winter.SeasonID, 2)));
        });
    }

    [Test]
    public async Task ARegeneratedSchedule_OrARemovedTeam_KeepsTheVod()
    {
        var vod = new Vod { YouTubeVideoID = "keptvideo00", Title = "SL24 Week 1 - Alpha vs Bravo", PublishedAt = new DateTime(2024, 3, 2), ChannelID = TestChannel, MatchStatus = VodMatchStatus.Confirmed, SeasonID = _season.SeasonID, SeasonMatchID = _week1.SeasonMatchID };
        vod.Teams.Add(new VodTeam { TeamID = _alpha.TeamID });
        vod.Teams.Add(new VodTeam { TeamID = _bravo.TeamID });
        _context.Vods.Add(vod);
        await _context.SaveChangesAsync();

        // As BracketService and MatchPlannerService regenerate them; as the import removes a team gone from its sheet
        await _context.SeasonMatches.Where(m => m.SeasonID == _season.SeasonID).ExecuteDeleteAsync();
        await _context.Teams.Where(t => t.TeamID == _bravo.TeamID).ExecuteDeleteAsync();

        var kept = await Row("keptvideo00");
        Assert.Multiple(() =>
        {
            Assert.That((kept.SeasonID, kept.SeasonMatchID), Is.EqualTo(((int?)_season.SeasonID, (int?)null)));
            Assert.That(kept.Teams.Select(t => t.TeamID), Is.EqualTo(new[] { _alpha.TeamID }));
        });
    }

    // Run by the historical jobs

    private async Task<(HistoricalImportRunner Runner, HistoricalImportJobs Jobs)> Runner(VodService vods)
    {
        var sources = new HistoricalSourceService(_context, new HistoricalSourceFetcher(new HttpClient(_handler)), TimeProvider.System, NullLogger<HistoricalSourceService>.Instance);
        await sources.EnsureCatalog();
        // Only the channel is downloaded
        await _context.HistoricalSources.Where(s => s.Kind != HistoricalSourceKind.YouTubeChannel).ExecuteUpdateAsync(s => s.SetProperty(x => x.Enabled, false));
        var import = Substitute.For<IHistoricalImportService>();
        import.Run(Arg.Any<HistoricalArchiveInput>(), Arg.Any<HistoricalImportRunOptions>(), Arg.Any<Func<HistoricalImportProgress, Task>?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new HistoricalImportReport()));
        var jobs = new HistoricalImportJobs(_context, new HistoricalImportSignal());
        return (new HistoricalImportRunner(_context, sources, import, jobs, Options.Create(new HistoricalGuardrailOptions()), NullLogger<HistoricalImportRunner>.Instance, vods), jobs);
    }

    [Test]
    public async Task ARefreshJob_FindsTheVods_OfTheChannelPageItDownloaded()
    {
        _page = YouTubeFixtures.ChannelPage(YouTubeFixtures.Lockup("week1video0", "SL24 Week 1 - Alpha vs Bravo", "2w ago", "1:50:01"));
        var (runner, jobs) = await Runner(Service());
        var jobId = await jobs.Enqueue(HistoricalImportJob.Refresh());

        var report = await runner.Run(jobId, default, _ => Task.CompletedTask, CancellationToken.None);

        var ids = await _context.Vods.Select(v => v.YouTubeVideoID).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(report.SourcesChanged, Does.Contain("youtube-grifball-channel"));
            Assert.That(ids, Does.Contain("week1video0"));
            Assert.That(ids, Is.SupersetOf(VodArchive.Videos.Select(v => v.VideoID)), "The catalog's channel is the league's");
            Assert.That(report.SourcesFailed, Has.Some.StartsWith("VODs: youtube-grifball-channel feed"));
        });
    }

    [Test]
    public async Task AVodFailure_IsReported_NotTheJobs()
    {
        var throwing = new FakeSourcesHandler(request => request.RequestUri!.ToString().Contains("/feeds/") ? throw new InvalidOperationException("boom") : FakeSourcesHandler.Status(HttpStatusCode.NotFound));
        var (runner, jobs) = await Runner(Service(handler: throwing));
        var jobId = await jobs.Enqueue(HistoricalImportJob.Refresh());

        var report = await runner.Run(jobId, default, _ => Task.CompletedTask, CancellationToken.None);

        Assert.That(report.SourcesFailed, Has.Some.StartsWith("VODs:"));
    }

    [Test]
    public async Task AnImportJob_MatchesTheVodsAgain()
    {
        _context.Vods.Add(new Vod { YouTubeVideoID = "week1video0", Title = "SL24 Week 1 - Alpha vs Bravo", PublishedAt = new DateTime(2024, 3, 2), ChannelID = TestChannel, MatchStatus = VodMatchStatus.NeedsReview });
        await _context.SaveChangesAsync();
        var (runner, jobs) = await Runner(Service());
        var preview = await jobs.Enqueue(HistoricalImportJob.Preview(""));
        var import = await jobs.Enqueue(HistoricalImportJob.ImportSnapshots(""));

        await runner.Run(preview, default, _ => Task.CompletedTask, CancellationToken.None);
        var afterPreview = (await Row("week1video0")).MatchStatus;
        await runner.Run(import, default, _ => Task.CompletedTask, CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(afterPreview, Is.EqualTo(VodMatchStatus.NeedsReview), "A dry run changes nothing");
            Assert.That((await Row("week1video0")).MatchStatus, Is.EqualTo(VodMatchStatus.Auto));
        });
    }

    [Test]
    public async Task AVod_ReadsBackWithItsSeasonMatchAndTeams()
    {
        var vod = new Vod { YouTubeVideoID = "roundtrip00", Title = "SL24 Week 1 - Alpha vs Bravo", PublishedAt = new DateTime(2024, 3, 2), ChannelID = TestChannel, MatchStatus = VodMatchStatus.Confirmed, SeasonID = _season.SeasonID, SeasonMatchID = _week1.SeasonMatchID };
        vod.Teams.Add(new VodTeam { TeamID = _alpha.TeamID });
        _context.Vods.Add(vod);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var row = await _context.Vods.AsNoTracking().Include(v => v.Season).Include(v => v.SeasonMatch).Include(v => v.Teams).ThenInclude(t => t.Team).SingleAsync(v => v.VodID == vod.VodID);
        var link = await _context.VodTeams.AsNoTracking().Include(t => t.Vod).SingleAsync(t => t.VodID == vod.VodID);

        Assert.Multiple(() =>
        {
            Assert.That(row.Season.SeasonName, Is.EqualTo("Spring League 2024"));
            Assert.That(row.SeasonMatch.SeasonMatchID, Is.EqualTo(_week1.SeasonMatchID));
            Assert.That(row.Teams.Single().Team.TeamName, Is.EqualTo("Alpha"));
            Assert.That((link.VodID, link.Vod.YouTubeVideoID), Is.EqualTo((vod.VodID, "roundtrip00")));
        });
    }

    [Test]
    public async Task Relinking_ToFewerTeams_TakesTheOthersOff()
    {
        var vod = new Vod { YouTubeVideoID = "relinked000", Title = "SL24 - Alpha vs Bravo", PublishedAt = new DateTime(2024, 3, 2), ChannelID = TestChannel, MatchStatus = VodMatchStatus.NeedsReview };
        _context.Vods.Add(vod);
        await _context.SaveChangesAsync();

        Assert.That(await Service().Link(vod.VodID, new LinkVodRequest { SeasonID = _season.SeasonID, TeamIDs = [_alpha.TeamID, _bravo.TeamID] }), Is.Null);
        Assert.That(await Service().Link(vod.VodID, new LinkVodRequest { SeasonID = _season.SeasonID, TeamIDs = [_alpha.TeamID] }), Is.Null);

        _context.ChangeTracker.Clear();
        Assert.That(await _context.VodTeams.Where(t => t.VodID == vod.VodID).Select(t => t.TeamID).ToListAsync(), Is.EqualTo(new[] { _alpha.TeamID }));
    }
}
