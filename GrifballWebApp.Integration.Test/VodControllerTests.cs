using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Historical.Vods;
using GrifballWebApp.Domain.League;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using GrifballWebApp.Application.Historical.Vods;

namespace GrifballWebApp.Integration.Test;

/// <summary>The VOD endpoints: the pages' lists for anyone, the review (link, dismiss, link options) for sysadmins.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class VodControllerTests
{
    private GrifballContext _context = null!;
    private VodController _controller = null!;
    private Season _season = null!;
    private Team _team = null!;
    private SeasonMatch _match = null!;
    private Vod _linked = null!;
    private Vod _unsure = null!;
    private int _userId;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var service = new VodService(_context, new YouTubeClient(new HttpClient(new FakeSourcesHandler(_ => FakeSourcesHandler.Status(HttpStatusCode.NotFound)))),
            Options.Create(new YouTubeOptions()), NullLogger<VodService>.Instance);
        _controller = new VodController(service);

        _season = await _context.AddSeason("Spring League 2024");
        var user = await _context.AddUser("player", gamertag: "Player");
        _userId = user.Id;
        _team = await _context.AddTeam(_season, "Alpha", user, 1);
        var other = Team.Seed(seasonID: _season.SeasonID, teamName: "Bravo");
        _context.Teams.Add(other);
        await _context.SaveChangesAsync();
        _match = SeasonMatch.Seed(_season.SeasonID, _team.TeamID, other.TeamID, bestOf: 3);
        _context.SeasonMatches.Add(_match);
        await _context.SaveChangesAsync();
        _linked = Vod.Seed("linkedvideo", "SL24 - Alpha vs Bravo", new DateTime(2025, 1, 2), VodMatchStatus.Auto, _season.SeasonID, _match.SeasonMatchID, [_team.TeamID]);
        _unsure = Vod.Seed("unsurevideo", "Who knows", new DateTime(2025, 1, 3), VodMatchStatus.NeedsReview);
        _context.Vods.AddRange(_linked, _unsure);
        await _context.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task ThePagesLists_HaveTheLinkedVod()
    {
        var ct = CancellationToken.None;

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await _controller.ForSeasonMatch(_match.SeasonMatchID, ct)).Select(v => v.YouTubeVideoID), Is.EqualTo(new[] { "linkedvideo" }));
            Assert.That((await _controller.ForSeason(_season.SeasonID, ct)).Select(v => v.YouTubeVideoID), Is.EqualTo(new[] { "linkedvideo" }));
            Assert.That((await _controller.ForTeam(_team.TeamID, ct)).Select(v => v.YouTubeVideoID), Is.EqualTo(new[] { "linkedvideo" }));
            Assert.That((await _controller.ForPlayer(_userId, ct)).Select(v => v.YouTubeVideoID), Is.EqualTo(new[] { "linkedvideo" }));
        }
    }

    [Test]
    public async Task Review_ListsTheUnsure_AndLinksOrDismissesThem()
    {
        var ct = CancellationToken.None;

        var review = await _controller.Review(ct);
        var seasons = await _controller.LinkSeasons(ct);
        var options = (await _controller.LinkOptions(_season.SeasonID, ct)).Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(review.Select(v => v.YouTubeVideoID), Is.EqualTo(new[] { "unsurevideo" }));
            Assert.That(seasons.Select(s => s.SeasonID), Does.Contain(_season.SeasonID));
            Assert.That(options!.Matches.Select(m => m.SeasonMatchID), Is.EqualTo(new[] { _match.SeasonMatchID }));
            Assert.That((await _controller.LinkOptions(-1, ct)).Result, Is.InstanceOf<NotFoundResult>());
            Assert.That(await _controller.Link(_unsure.VodID, new LinkVodRequest(), ct), Is.InstanceOf<BadRequestObjectResult>());
            Assert.That(await _controller.Link(-1, new LinkVodRequest { SeasonID = _season.SeasonID }, ct), Is.InstanceOf<NotFoundResult>());
            Assert.That(await _controller.Link(_unsure.VodID, new LinkVodRequest { SeasonMatchID = _match.SeasonMatchID }, ct), Is.InstanceOf<NoContentResult>());
            Assert.That(await _controller.Dismiss(_linked.VodID, ct), Is.InstanceOf<NoContentResult>());
            Assert.That(await _controller.Dismiss(-1, ct), Is.InstanceOf<NotFoundResult>());
            Assert.That((await _controller.ForSeasonMatch(_match.SeasonMatchID, ct)).Select(v => v.YouTubeVideoID), Is.EqualTo(new[] { "unsurevideo" }), "Linked by hand; the dismissed one hidden");
        }
    }
}
