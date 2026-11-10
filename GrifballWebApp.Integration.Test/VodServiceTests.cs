using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using System.Text;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Infrastructure.Historical;
using GrifballWebApp.Infrastructure.Historical.Sources;

namespace GrifballWebApp.Integration.Test;

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
    private static readonly string[] expected = new[] { "week1video0", "chatvideo00" };
    private static readonly string[] expectedArray = new[] { "week1video0" };
    private static readonly string[] expectedArray0 = new[] { "linkedvideo" };
    private static readonly string[] expectedArray1 = new[] { "Alpha", "Bravo" };
    private static readonly string[] expectedArray2 = new[] { "confirmedvd", "linkedvideo" };
    private static readonly string[] expectedArray3 = new[] { "linkedvideo" };

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

        _season = Season.Seed(seasonName: "Spring League 2024").WithDates(seasonStart: new DateTime(2024, 3, 1), seasonEnd: new DateTime(2024, 5, 31));
        _context.Seasons.Add(_season);
        await _context.SaveChangesAsync();
        _alpha = Team.Seed(seasonID: _season.SeasonID, teamName: "Alpha");
        _bravo = Team.Seed(seasonID: _season.SeasonID, teamName: "Bravo");
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.Vods.Select(v => v.YouTubeVideoID).ToListAsync(), Is.EquivalentTo(expected), "No clip, no Short");
            Assert.That((result.Added, result.Linked, result.NeedsReview, result.Failed.Count), Is.EqualTo((2, 1, 1, 0)));
            Assert.That((week1.PublishedAt, week1.PublishedAtIsApproximate), Is.EqualTo((new DateTime(2024, 3, 2, 20, 0, 0), false)), "The feed's exact date over the page's 2w ago");
            Assert.That(week1.DurationSeconds, Is.EqualTo(6601), "The page's duration");
            Assert.That(week1.Description, Is.EqualTo("Cast by Rey"));
            Assert.That(week1.ChannelID, Is.EqualTo(TestChannel));
            Assert.That((week1.MatchStatus, week1.SeasonID, week1.SeasonMatchID), Is.EqualTo((VodMatchStatus.Auto, (int?)_season.SeasonID, (int?)_week1.SeasonMatchID)));
            Assert.That(week1.Teams.Select(t => t.TeamID), Is.EquivalentTo(new[] { _alpha.TeamID, _bravo.TeamID }));
            Assert.That((chat.MatchStatus, chat.SeasonID), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)_season.SeasonID)), "Suggested: running then, no teams");
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That((first.Added, second.Added, second.Updated, third.Added, third.Updated), Is.EqualTo((1, 0, 1, 0, 0)));
            Assert.That(await _context.Vods.CountAsync(), Is.EqualTo(1), "One row per video ID");
            Assert.That((approximate.PublishedAt, approximate.PublishedAtIsApproximate), Is.EqualTo((new DateTime(2024, 3, 2), true)));
            Assert.That((exact.PublishedAt, exact.PublishedAtIsApproximate), Is.EqualTo((new DateTime(2024, 3, 2, 20, 0, 0), false)));
            Assert.That(exact.Title, Is.EqualTo("SL24 Week 1 - Alpha vs Bravo (full cast)"));
            Assert.That(exact.Description, Is.EqualTo("Cast by Rey"));
        }
    }

    [Test]
    public async Task Sync_ReportsWhatCouldntBeRead_AndKeepsTheRest()
    {
        await ChannelWithVideos();
        _feed = null;
        await AddChannel("not-a-channel-id", "broken-channel");

        var result = await Service().Sync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Failed, Has.Some.Contains($"{SourceID} feed: the feed answered 404"));
            Assert.That(result.Failed, Has.Some.Contains("broken-channel: no channel ID"));
            Assert.That(await _context.Vods.Select(v => v.YouTubeVideoID).ToListAsync(), Is.EqualTo(expectedArray), "The page's VOD still");
        }
    }

    [Test]
    public async Task Sync_WithAnApiKey_ReadsEveryUpload_AndTheirDurations()
    {
        await AddChannel();
        _playlist = YouTubeFixtures.PlaylistItems(null, ("week1video0", "SL24 Week 1 - Alpha vs Bravo", "2024-03-02T20:00:00Z"), ("clipvideo00", "Highlight", "2024-03-03T20:00:00Z"));
        _videos = YouTubeFixtures.Videos(("week1video0", "SL24 Week 1 - Alpha vs Bravo", "2024-03-02T20:00:00Z", "PT1H50M1S"), ("clipvideo00", "Highlight", "2024-03-03T20:00:00Z", "PT2M"));

        var result = await Service("test-key").Sync();

        var week1 = await Row("week1video0");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.Vods.Select(v => v.YouTubeVideoID).ToListAsync(), Is.EqualTo(expectedArray), "Two minutes is a clip");
            Assert.That((week1.DurationSeconds, week1.PublishedAtIsApproximate, week1.MatchStatus), Is.EqualTo(((int?)6601, false, VodMatchStatus.Auto)));
            Assert.That(_handler.Requests, Has.Some.Matches<string>(r => r.Contains("/playlistItems?") && r.Contains("key=test-key")));
            Assert.That(_handler.Requests, Has.Some.Matches<string>(r => r.Contains("/videos?") && r.Contains("week1video0")));
            Assert.That(result.Failed, Has.Some.Contains("feed"), "No feed answered; the API's videos still count");
        }

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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Failed, Has.Some.Contains("uploads: the Data API answered 403"));
            Assert.That(result.Failed, Has.Some.Contains("YouTube videos: the Data API answered 403"));
            Assert.That(await _context.Vods.CountAsync(), Is.EqualTo(2), "The page and the feed still count");
        }
    }

    [Test]
    public async Task Sync_BackfillsTheArchivesCapture_ForTheLeaguesChannel()
    {
        await AddChannel(VodArchive.ChannelID, "youtube-grifball-channel");
        // One of the IDs the archive found without a title, which only the API can name
        _videos = YouTubeFixtures.Videos(("8IjYNH7Vsso", "WL23 Grand Final - Alpha vs Bravo", "2023-03-01T00:00:00Z", "PT1H"));

        await Service("test-key").Sync();

        var ids = await _context.Vods.Select(v => v.YouTubeVideoID).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ids, Is.SupersetOf(VodArchive.Videos.Select(v => v.VideoID)));
            Assert.That(ids, Does.Contain("8IjYNH7Vsso"));
            Assert.That(ids, Has.Count.EqualTo(VodArchive.Videos.Count + 1), "The Shorts' IDs aren't kept");
            Assert.That(_handler.Requests, Has.Some.Matches<string>(r => r.Contains("/videos?") && r.Contains("v327Rjqsij8")), "Every archive ID asked about");
            Assert.That((await Row("Hb2JmxJhQd8")).PublishedAtIsApproximate, Is.True);
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(review.Select(r => (r.YouTubeVideoID, r.MatchStatus, r.SeasonName)), Is.EqualTo(new[] { ("chatvideo00", "NeedsReview", "Spring League 2024") }));
            Assert.That(review[0].MatchNote, Does.Contain("No teams"));

            Assert.That(await service.Link(chat.VodID, new LinkVodRequest { SeasonID = _season.SeasonID, TeamIDs = [_alpha.TeamID] }), Is.Null);
        }
        var week1 = await Row("week1video0");
        Assert.That(await service.Dismiss(week1.VodID), Is.True);
        _context.ChangeTracker.Clear();

        // A later refresh leaves both decisions alone
        await service.Sync();

        var linked = await Row("chatvideo00");
        var dismissed = await Row("week1video0");
        using (Assert.EnterMultipleScope())
        {
            Assert.That((linked.MatchStatus, linked.SeasonID), Is.EqualTo((VodMatchStatus.Confirmed, (int?)_season.SeasonID)));
            Assert.That(linked.Teams.Select(t => t.TeamID), Is.EqualTo(new[] { _alpha.TeamID }));
            Assert.That(dismissed.MatchStatus, Is.EqualTo(VodMatchStatus.Dismissed), "Never suggested again");
            Assert.That(await service.Review(), Is.Empty);
            Assert.That(await service.ForSeason(_season.SeasonID), Has.Count.EqualTo(1), "The dismissed VOD is hidden");
            Assert.That(await service.Dismiss(-1), Is.False);
        }
    }

    [Test]
    public async Task Link_ChecksWhatItIsGiven()
    {
        var other = Season.Seed(seasonName: "Spring League 2025").WithDates(seasonStart: new DateTime(2025, 3, 1), seasonEnd: new DateTime(2025, 5, 31));
        _context.Seasons.Add(other);
        var vod = Vod.Seed("somevideo00", "Something", new DateTime(2024, 3, 2), VodMatchStatus.NeedsReview, channelID: TestChannel);
        _context.Vods.Add(vod);
        await _context.SaveChangesAsync();
        var otherTeam = Team.Seed(seasonID: other.SeasonID, teamName: "Charlie");
        _context.Teams.Add(otherTeam);
        await _context.SaveChangesAsync();
        var service = Service();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await service.Link(vod.VodID, new LinkVodRequest()), Is.EqualTo("Pick a season or a season match."));
            Assert.That(await service.Link(vod.VodID, new LinkVodRequest { SeasonID = -1 }), Is.EqualTo("There is no such season."));
            Assert.That(await service.Link(vod.VodID, new LinkVodRequest { SeasonMatchID = -1 }), Is.EqualTo("There is no such season match."));
            Assert.That(await service.Link(vod.VodID, new LinkVodRequest { SeasonID = other.SeasonID, SeasonMatchID = _week1.SeasonMatchID }), Is.EqualTo("The season match isn't in that season."));
            Assert.That(await service.Link(vod.VodID, new LinkVodRequest { SeasonID = _season.SeasonID, TeamIDs = [otherTeam.TeamID] }), Is.EqualTo("Every team must be one of the season's."));
        }
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.Link(-1, new LinkVodRequest { SeasonID = _season.SeasonID }));

        // A match alone: its season and teams follow
        Assert.That(await service.Link(vod.VodID, new LinkVodRequest { SeasonMatchID = _week1.SeasonMatchID }), Is.Null);
        var linked = await Row("somevideo00");
        using (Assert.EnterMultipleScope())
        {
            Assert.That((linked.MatchStatus, linked.SeasonID, linked.SeasonMatchID), Is.EqualTo((VodMatchStatus.Confirmed, (int?)_season.SeasonID, (int?)_week1.SeasonMatchID)));
            Assert.That(linked.Teams.Select(t => t.TeamID), Is.EquivalentTo(new[] { _alpha.TeamID, _bravo.TeamID }));
        }
    }

    [Test]
    public async Task Pages_ShowLinkedVods_Only()
    {
        var player = await _context.AddUser("player", gamertag: "Player");
        _context.Set<TeamPlayer>().Add(TeamPlayer.Seed(teamID: _alpha.TeamID, userID: player.Id));
        var linked = Vod.Seed("linkedvideo", "SL24 Week 1 - Alpha vs Bravo", new DateTime(2024, 3, 2), VodMatchStatus.Auto, _season.SeasonID, _week1.SeasonMatchID, [_alpha.TeamID, _bravo.TeamID], TestChannel, durationSeconds: 600);
        var confirmed = Vod.Seed("confirmedvd", "Bravo recap", new DateTime(2024, 3, 10), VodMatchStatus.Confirmed, _season.SeasonID, teamIDs: [_bravo.TeamID], channelID: TestChannel);
        var unsure = Vod.Seed("unsurevideo", "Alpha vs ?", new DateTime(2024, 3, 11), VodMatchStatus.NeedsReview, _season.SeasonID, _week1.SeasonMatchID, [_alpha.TeamID], TestChannel);
        _context.Vods.AddRange(linked, confirmed, unsure);
        await _context.SaveChangesAsync();
        var service = Service();

        var match = await service.ForSeasonMatch(_week1.SeasonMatchID);
        var season = await service.ForSeason(_season.SeasonID);
        var alpha = await service.ForTeam(_alpha.TeamID);
        var bravo = await service.ForTeam(_bravo.TeamID);
        var profile = await service.ForPlayer(player.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(match.Select(v => v.YouTubeVideoID), Is.EqualTo(expectedArray0));
            Assert.That(match[0].Teams.Select(t => t.TeamName), Is.EqualTo(expectedArray1));
            Assert.That((match[0].SeasonName, match[0].DurationSeconds), Is.EqualTo(("Spring League 2024", (int?)600)));
            Assert.That(season.Select(v => v.YouTubeVideoID), Is.EqualTo(expectedArray2), "Newest first");
            Assert.That(alpha.Select(v => v.YouTubeVideoID), Is.EqualTo(expectedArray0));
            Assert.That(bravo.Select(v => v.YouTubeVideoID), Is.EqualTo(expectedArray2));
            Assert.That(profile.Select(v => v.YouTubeVideoID), Is.EqualTo(expectedArray3), "Through the player's team");
        }
    }

    [Test]
    public async Task LinkOptions_AreTheSeasonsTeamsAndMatches()
    {
        var service = Service();

        var seasons = await service.LinkSeasons();
        var options = await service.LinkOptions(_season.SeasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(seasons.Select(s => s.SeasonName), Does.Contain("Spring League 2024"));
            Assert.That(options!.Teams.Select(t => t.TeamName), Is.EqualTo(expectedArray1));
            Assert.That(options.Matches.Single().Label, Is.EqualTo("Alpha vs Bravo · Mar 1, 2024"));
            Assert.That(options.Matches.Single().TeamIDs, Is.EqualTo(new[] { _alpha.TeamID, _bravo.TeamID }));
            Assert.That(await service.LinkOptions(-1), Is.Null);
        }
    }

    [Test]
    public async Task Rematch_LinksAVod_OnceItsSeasonIsImported()
    {
        _context.Vods.Add(Vod.Seed("latervideo0", "WL23 - Echo vs Foxtrot", new DateTime(2023, 2, 1), VodMatchStatus.NeedsReview, channelID: TestChannel));
        await _context.SaveChangesAsync();
        var service = Service();
        var before = await service.Rematch();

        var winter = Season.Seed(seasonName: "Winter League 2023 (DA)", historicalSourceID: "wb/wl23da").WithDates(seasonStart: new DateTime(2023, 1, 1), seasonEnd: new DateTime(2023, 12, 31));
        _context.Seasons.Add(winter);
        await _context.SaveChangesAsync();
        _context.Teams.AddRange(Team.Seed(seasonID: winter.SeasonID, teamName: "Echo"), Team.Seed(seasonID: winter.SeasonID, teamName: "Foxtrot"));
        await _context.SaveChangesAsync();
        var after = await service.Rematch();

        var vod = await Row("latervideo0");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(before, Is.EqualTo((0, 1)));
            Assert.That(after, Is.EqualTo((1, 0)));
            Assert.That((vod.MatchStatus, vod.SeasonID, vod.Teams.Count), Is.EqualTo((VodMatchStatus.Auto, (int?)winter.SeasonID, 2)));
        }
    }

    [Test]
    public async Task ARegeneratedSchedule_OrARemovedTeam_KeepsTheVod()
    {
        var vod = Vod.Seed("keptvideo00", "SL24 Week 1 - Alpha vs Bravo", new DateTime(2024, 3, 2), VodMatchStatus.Confirmed, _season.SeasonID, _week1.SeasonMatchID, [_alpha.TeamID, _bravo.TeamID], TestChannel);
        _context.Vods.Add(vod);
        await _context.SaveChangesAsync();

        // As BracketService and MatchPlannerService regenerate them; as the import removes a team gone from its sheet
        await _context.SeasonMatches.Where(m => m.SeasonID == _season.SeasonID).ExecuteDeleteAsync();
        await _context.Teams.Where(t => t.TeamID == _bravo.TeamID).ExecuteDeleteAsync();

        var kept = await Row("keptvideo00");
        using (Assert.EnterMultipleScope())
        {
            Assert.That((kept.SeasonID, kept.SeasonMatchID), Is.EqualTo(((int?)_season.SeasonID, (int?)null)));
            Assert.That(kept.Teams.Select(t => t.TeamID), Is.EqualTo(new[] { _alpha.TeamID }));
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.SourcesChanged, Does.Contain("youtube-grifball-channel"));
            Assert.That(ids, Does.Contain("week1video0"));
            Assert.That(ids, Is.SupersetOf(VodArchive.Videos.Select(v => v.VideoID)), "The catalog's channel is the league's");
            Assert.That(report.SourcesFailed, Has.Some.StartsWith("VODs: youtube-grifball-channel feed"));
        }
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
        _context.Vods.Add(Vod.Seed("week1video0", "SL24 Week 1 - Alpha vs Bravo", new DateTime(2024, 3, 2), VodMatchStatus.NeedsReview, channelID: TestChannel));
        await _context.SaveChangesAsync();
        var (runner, jobs) = await Runner(Service());
        var preview = await jobs.Enqueue(HistoricalImportJob.Preview(""));
        var import = await jobs.Enqueue(HistoricalImportJob.ImportSnapshots(""));

        await runner.Run(preview, default, _ => Task.CompletedTask, CancellationToken.None);
        var afterPreview = (await Row("week1video0")).MatchStatus;
        await runner.Run(import, default, _ => Task.CompletedTask, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterPreview, Is.EqualTo(VodMatchStatus.NeedsReview), "A dry run changes nothing");
            Assert.That((await Row("week1video0")).MatchStatus, Is.EqualTo(VodMatchStatus.Auto));
        }
    }

    [Test]
    public async Task AVod_ReadsBackWithItsSeasonMatchAndTeams()
    {
        var vod = Vod.Seed("roundtrip00", "SL24 Week 1 - Alpha vs Bravo", new DateTime(2024, 3, 2), VodMatchStatus.Confirmed, _season.SeasonID, _week1.SeasonMatchID, [_alpha.TeamID], TestChannel);
        _context.Vods.Add(vod);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var row = await _context.Vods.AsNoTracking().Include(v => v.Season).Include(v => v.SeasonMatch).Include(v => v.Teams).ThenInclude(t => t.Team).SingleAsync(v => v.VodID == vod.VodID);
        var link = await _context.Set<VodTeam>().AsNoTracking().Include(t => t.Vod).SingleAsync(t => t.VodID == vod.VodID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.Season!.SeasonName, Is.EqualTo("Spring League 2024"));
            Assert.That(row.SeasonMatch!.SeasonMatchID, Is.EqualTo(_week1.SeasonMatchID));
            Assert.That(row.Teams.Single().Team.TeamName, Is.EqualTo("Alpha"));
            Assert.That((link.VodID, link.Vod.YouTubeVideoID), Is.EqualTo((vod.VodID, "roundtrip00")));
        }
    }

    [Test]
    public async Task Relinking_ToFewerTeams_TakesTheOthersOff()
    {
        var vod = Vod.Seed("relinked000", "SL24 - Alpha vs Bravo", new DateTime(2024, 3, 2), VodMatchStatus.NeedsReview, channelID: TestChannel);
        _context.Vods.Add(vod);
        await _context.SaveChangesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Service().Link(vod.VodID, new LinkVodRequest { SeasonID = _season.SeasonID, TeamIDs = [_alpha.TeamID, _bravo.TeamID] }), Is.Null);
            Assert.That(await Service().Link(vod.VodID, new LinkVodRequest { SeasonID = _season.SeasonID, TeamIDs = [_alpha.TeamID] }), Is.Null);
        }

        _context.ChangeTracker.Clear();
        Assert.That(await _context.Set<VodTeam>().Where(t => t.VodID == vod.VodID).Select(t => t.TeamID).ToListAsync(), Is.EqualTo(new[] { _alpha.TeamID }));
    }
}
