using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

[TestFixture]
internal sealed class VodServiceUnitTests : ApplicationDbContextTests
{
    private VodService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var youtube = new YouTubeClient(new HttpClient(new FakeSourcesHandler(_ => throw new InvalidOperationException("No HTTP call is expected in these tests."))));
        _service = new VodService(Context, youtube, Options.Create(new YouTubeOptions()), Substitute.For<ILogger<VodService>>());
    }

    private static Vod Linked(string videoId, int seasonID, int? seasonMatchID = null, IEnumerable<int>? teamIDs = null)
        => Vod.Seed(videoId, $"Title {videoId}", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), VodMatchStatus.Auto, seasonID, seasonMatchID, teamIDs);

    private static readonly string[] OnlyV1 = ["v1"];
    private static readonly int[] NewestSeasonFirst = [2, 1];
    private static readonly int[] HomeAndAwayTeamIDs = [10, 20];

    [Test]
    public async Task ForSeason_ReturnsVisibleOnly()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: 1));
        Context.Vods.Add(Linked("v1", 1));
        Context.Vods.Add(Vod.Seed("v2", "Needs review", new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc), VodMatchStatus.NeedsReview, 1));
        await Context.SaveChangesAsync();

        var result = await _service.ForSeason(1, CancellationToken.None);

        Assert.That(result.Select(v => v.YouTubeVideoID), Is.EqualTo(OnlyV1));
    }

    [Test]
    public async Task ForSeasonMatch_ReturnsMatchingVisible()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: 1));
        var home = Team.Seed(teamName: "Home", seasonID: 1, teamID: 10);
        var away = Team.Seed(teamName: "Away", seasonID: 1, teamID: 20);
        Context.Teams.AddRange(home, away);
        var match1 = SeasonMatch.Between(1, 10, 20, bestOf: 1);
        var match2 = SeasonMatch.Between(1, 10, 20, bestOf: 1);
        Context.SeasonMatches.AddRange(match1, match2);
        await Context.SaveChangesAsync();
        Context.Vods.Add(Linked("v1", 1, seasonMatchID: match1.SeasonMatchID));
        Context.Vods.Add(Linked("v2", 1, seasonMatchID: match2.SeasonMatchID));
        await Context.SaveChangesAsync();

        var result = await _service.ForSeasonMatch(match1.SeasonMatchID, CancellationToken.None);

        Assert.That(result.Select(v => v.YouTubeVideoID), Is.EqualTo(OnlyV1));
    }

    [Test]
    public async Task ForTeam_ReturnsVodsLinkedToTeam()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: 1));
        var team = Team.Seed(teamName: "Reclaimers", seasonID: 1, teamID: 7);
        var otherTeam = Team.Seed(teamName: "Other", seasonID: 1, teamID: 8);
        Context.Teams.AddRange(team, otherTeam);
        Context.Vods.Add(Linked("v1", 1, teamIDs: [7]));
        Context.Vods.Add(Linked("v2", 1, teamIDs: [8]));
        await Context.SaveChangesAsync();

        var result = await _service.ForTeam(7, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Select(v => v.YouTubeVideoID), Is.EqualTo(OnlyV1));
            Assert.That(result[0].Teams.Single().TeamID, Is.EqualTo(7));
        }
    }

    [Test]
    public async Task ForPlayer_ReturnsVodsOfTeamsThePlayerPlayedOn()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: 1));
        var team = Team.Seed(teamName: "Reclaimers", seasonID: 1, teamID: 7);
        Context.Teams.Add(team);
        Context.Users.Add(User.Seed(userName: "player", id: 50));
        Context.Add(TeamPlayer.Seed(teamID: 7, userID: 50));
        Context.Vods.Add(Linked("v1", 1, teamIDs: [7]));
        await Context.SaveChangesAsync();

        var result = await _service.ForPlayer(50, CancellationToken.None);

        Assert.That(result.Select(v => v.YouTubeVideoID), Is.EqualTo(OnlyV1));
    }

    [Test]
    public async Task Review_ListsNeedsReviewVods()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: 1));
        Context.Vods.Add(Vod.Seed("v1", "Needs review", new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc), VodMatchStatus.NeedsReview));
        Context.Vods.Add(Linked("v2", 1)); // Auto needs a season (CK_Event_Vods_LinkedHasSeason)
        await Context.SaveChangesAsync();

        var result = await _service.Review(CancellationToken.None);

        Assert.That(result.Select(v => v.YouTubeVideoID), Is.EqualTo(OnlyV1));
    }

    [Test]
    public async Task LinkSeasons_ReturnsSeasonsNewestFirst()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "Old", seasonID: 1));
        var newer = Season.Seed(seasonName: "New", seasonID: 2);
        newer.Schedule(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 1, 3, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        Context.Seasons.Add(newer);
        await Context.SaveChangesAsync();

        var result = await _service.LinkSeasons(CancellationToken.None);

        Assert.That(result.Select(s => s.SeasonID), Is.EqualTo(NewestSeasonFirst));
    }

    [Test]
    public async Task LinkOptions_NoSuchSeason_ReturnsNull()
    {
        var result = await _service.LinkOptions(999, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task LinkOptions_ReturnsTeamsAndMatches()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: 1));
        var home = Team.Seed(teamName: "Home", seasonID: 1, teamID: 10);
        var away = Team.Seed(teamName: "Away", seasonID: 1, teamID: 20);
        Context.Teams.AddRange(home, away);
        Context.SeasonMatches.Add(SeasonMatch.Between(1, 10, 20, bestOf: 3));
        await Context.SaveChangesAsync();

        var result = await _service.LinkOptions(1, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.Teams, Has.Count.EqualTo(2));
            Assert.That(result.Matches, Has.Count.EqualTo(1));
            Assert.That(result.Matches[0].Label, Does.Contain("Home"));
            Assert.That(result.Matches[0].Label, Does.Contain("Away"));
        }
    }

    [Test]
    public async Task Link_NoSuchVod_Throws()
    {
        Assert.That(async () => await _service.Link(999, new LinkVodRequest(), CancellationToken.None), Throws.TypeOf<KeyNotFoundException>());
    }

    [Test]
    public async Task Link_NoSeasonOrMatchGiven_ReturnsMessage()
    {
        Context.Vods.Add(Vod.Seed("v1", "Title", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), VodMatchStatus.NeedsReview));
        await Context.SaveChangesAsync();
        var vod = await Context.Vods.SingleAsync();

        var result = await _service.Link(vod.VodID, new LinkVodRequest(), CancellationToken.None);

        Assert.That(result, Is.EqualTo("Pick a season or a season match."));
    }

    [Test]
    public async Task Link_NoSuchSeason_ReturnsMessage()
    {
        Context.Vods.Add(Vod.Seed("v1", "Title", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), VodMatchStatus.NeedsReview));
        await Context.SaveChangesAsync();
        var vod = await Context.Vods.SingleAsync();

        var result = await _service.Link(vod.VodID, new LinkVodRequest { SeasonID = 999 }, CancellationToken.None);

        Assert.That(result, Is.EqualTo("There is no such season."));
    }

    [Test]
    public async Task Link_BySeasonMatch_ConfirmsWithBothTeams()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: 1));
        var home = Team.Seed(teamName: "Home", seasonID: 1, teamID: 10);
        var away = Team.Seed(teamName: "Away", seasonID: 1, teamID: 20);
        Context.Teams.AddRange(home, away);
        var match = SeasonMatch.Between(1, 10, 20, bestOf: 3);
        Context.SeasonMatches.Add(match);
        Context.Vods.Add(Vod.Seed("v1", "Title", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), VodMatchStatus.NeedsReview));
        await Context.SaveChangesAsync();
        var vod = await Context.Vods.SingleAsync();

        var result = await _service.Link(vod.VodID, new LinkVodRequest { SeasonMatchID = match.SeasonMatchID }, CancellationToken.None);

        Assert.That(result, Is.Null);
        var reloaded = await Context.Vods.Include(v => v.Teams).SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reloaded.MatchStatus, Is.EqualTo(VodMatchStatus.Confirmed));
            Assert.That(reloaded.SeasonID, Is.EqualTo(1));
            Assert.That(reloaded.Teams.Select(t => t.TeamID), Is.EquivalentTo(HomeAndAwayTeamIDs));
        }
    }

    [Test]
    public async Task Link_TeamNotInSeason_ReturnsMessage()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: 1));
        Context.Seasons.Add(Season.Seed(seasonName: "S2", seasonID: 2));
        Context.Teams.Add(Team.Seed(teamName: "Other", seasonID: 2, teamID: 99));
        Context.Vods.Add(Vod.Seed("v1", "Title", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), VodMatchStatus.NeedsReview));
        await Context.SaveChangesAsync();
        var vod = await Context.Vods.SingleAsync();

        var result = await _service.Link(vod.VodID, new LinkVodRequest { SeasonID = 1, TeamIDs = [99] }, CancellationToken.None);

        Assert.That(result, Is.EqualTo("Every team must be one of the season's."));
    }

    [Test]
    public async Task Dismiss_NoSuchVod_ReturnsFalse()
    {
        var result = await _service.Dismiss(999, CancellationToken.None);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task Dismiss_Existing_HidesIt()
    {
        Context.Vods.Add(Vod.Seed("v1", "Title", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), VodMatchStatus.NeedsReview));
        await Context.SaveChangesAsync();
        var vod = await Context.Vods.SingleAsync();

        var result = await _service.Dismiss(vod.VodID, CancellationToken.None);

        Assert.That(result, Is.True);
        var reloaded = await Context.Vods.SingleAsync();
        Assert.That(reloaded.MatchStatus, Is.EqualTo(VodMatchStatus.Dismissed));
    }

    [Test]
    public async Task Rematch_NothingMatches_StaysNeedsReview()
    {
        Context.Vods.Add(Vod.Seed("v1", "Some Random Stream Title", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), VodMatchStatus.NeedsReview));
        await Context.SaveChangesAsync();

        var (linked, needsReview) = await _service.Rematch(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(linked, Is.Zero);
            Assert.That(needsReview, Is.EqualTo(1));
        }
    }
}
