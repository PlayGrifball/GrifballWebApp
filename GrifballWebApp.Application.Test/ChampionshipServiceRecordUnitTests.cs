using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Test;

/// <summary>ChampionshipService's database-backed methods (RecordChampionsAsync, AwardFromBracketAsync) against the
/// fake IApplicationDbContext. ChampionOf (pure) is ChampionshipServiceUnitTests'; this file doesn't repeat it.</summary>
[TestFixture]
internal sealed class ChampionshipServiceRecordUnitTests : ApplicationDbContextTests
{
    private const int SeasonID = 1;
    private ChampionshipService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new ChampionshipService(Context);
    }

    private async Task<Team> SeedTeamWithRoster(int captainUserId, int pickUserId)
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: SeasonID));
        var team = Team.Seed(seasonID: SeasonID, teamName: "Champions");
        Context.Teams.Add(team);
        Context.Users.AddRange(User.Seed(userName: "captain", id: captainUserId), User.Seed(userName: "pick", id: pickUserId));
        await Context.SaveChangesAsync();
        Context.Add(TeamPlayer.Seed(teamID: team.TeamID, userID: captainUserId));
        Context.Add(TeamPlayer.Seed(teamID: team.TeamID, userID: pickUserId));
        await Context.SaveChangesAsync();
        team.SetCaptain(captainUserId);
        await Context.SaveChangesAsync();
        return team;
    }

    [Test]
    public async Task RecordChampionsAsync_ImportedSeason_ReturnsNullAndChangesNothing()
    {
        var season = Season.Imported("src-1", "Imported Season", leagueID: 1, gameVersionID: null, division: null, historicalSourceUrl: "http://x", format: SeasonFormat.LeagueSeason);
        Context.Seasons.Add(season);
        var team = Team.Seed(seasonID: season.SeasonID, teamName: "Champs", season: season);
        Context.Teams.Add(team);
        await Context.SaveChangesAsync();

        var changes = await _service.RecordChampionsAsync(season.SeasonID, team.TeamID);
        Context.ChangeTracker.Clear();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changes, Is.Null);
            Assert.That(await Context.Set<PlayerChampionship>().CountAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task RecordChampionsAsync_TeamWithRoster_InsertsTitlesAndFlagsTheCaptain()
    {
        var team = await SeedTeamWithRoster(captainUserId: 1, pickUserId: 2);

        var changes = await _service.RecordChampionsAsync(SeasonID, team.TeamID);
        Context.ChangeTracker.Clear();

        var titles = await Context.Set<PlayerChampionship>().Where(c => c.SeasonID == SeasonID).ToListAsync();
        var captainTitle = titles.Single(c => c.UserID == 1);
        var pickTitle = titles.Single(c => c.UserID == 2);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(changes!.Inserted, Is.EqualTo(2));
            Assert.That(captainTitle.IsCaptain, Is.True);
            Assert.That(captainTitle.Tier, Is.EqualTo(ChampionshipService.DefaultTier));
            Assert.That(captainTitle.TeamID, Is.EqualTo(team.TeamID));
            Assert.That(pickTitle.IsCaptain, Is.False);
        }
    }

    [Test]
    public async Task RecordChampionsAsync_RunTwice_IsIdempotent()
    {
        var team = await SeedTeamWithRoster(captainUserId: 1, pickUserId: 2);
        await _service.RecordChampionsAsync(SeasonID, team.TeamID);
        // Each request gets a fresh context: the next call loads what the first one saved, not its tracked rows
        Context.ChangeTracker.Clear();

        var changes = await _service.RecordChampionsAsync(SeasonID, team.TeamID);
        Context.ChangeTracker.Clear();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changes!.Inserted, Is.Zero);
            Assert.That(changes.Updated, Is.EqualTo(2));
            Assert.That(await Context.Set<PlayerChampionship>().CountAsync(), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task RecordChampionsAsync_NullTeam_RemovesExistingTitles()
    {
        var team = await SeedTeamWithRoster(captainUserId: 1, pickUserId: 2);
        await _service.RecordChampionsAsync(SeasonID, team.TeamID);
        // Each request gets a fresh context: the next call loads what the first one saved, not its tracked rows
        Context.ChangeTracker.Clear();

        var changes = await _service.RecordChampionsAsync(SeasonID, null);
        Context.ChangeTracker.Clear();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changes!.Removed, Is.EqualTo(2));
            Assert.That(await Context.Set<PlayerChampionship>().CountAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task AwardFromBracketAsync_DecidedWinnerBracketFinal_AwardsTheWinner()
    {
        var team = await SeedTeamWithRoster(captainUserId: 1, pickUserId: 2);
        var other = Team.Seed(seasonID: SeasonID, teamName: "Runners-up");
        Context.Teams.Add(other);
        await Context.SaveChangesAsync();
        var slot = MatchBracketInfo.Seeded(Bracket.Winner, roundNumber: 1, matchNumber: 1, homeSeed: 1, awaySeed: 2);
        var match = SeasonMatch.Seed(SeasonID, team.TeamID, other.TeamID, bracketMatch: slot).WonBy(SeasonMatchSide.Home);
        Context.SeasonMatches.Add(match);
        await Context.SaveChangesAsync();

        var changes = await _service.AwardFromBracketAsync(SeasonID);
        Context.ChangeTracker.Clear();

        var title = await Context.Set<PlayerChampionship>().SingleAsync(c => c.UserID == 1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(changes!.Inserted, Is.EqualTo(2));
            Assert.That(title.TeamID, Is.EqualTo(team.TeamID));
        }
    }

    [Test]
    public async Task AwardFromBracketAsync_NoBracketMatches_ReturnsNoChangesAndAwardsNobody()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: SeasonID));
        await Context.SaveChangesAsync();

        var changes = await _service.AwardFromBracketAsync(SeasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changes!.Inserted, Is.Zero);
            Assert.That(changes.Updated, Is.Zero);
            Assert.That(changes.Removed, Is.Zero);
        }
    }
}
