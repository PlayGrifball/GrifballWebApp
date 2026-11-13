using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Application.Test;

/// <summary>MatchDetailsService against the fake IApplicationDbContext: one Halo Infinite match's box score. Its
/// query is Include/ThenInclude with AsSplitQuery throughout (no projected nested collections), so it has no SQLite
/// APPLY gap.</summary>
[TestFixture]
internal sealed class MatchDetailsServiceUnitTests : ApplicationDbContextTests
{
    private MatchDetailsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new MatchDetailsService(Context);
    }

    private async Task SeedMedals()
    {
        Context.MedalTypes.Add(MedalType.Imported(1, "Multikill"));
        Context.MedalDifficulties.Add(MedalDifficulty.Imported(1, "Normal"));
        Context.Medals.Add(Medal.Imported(1, "Double Kill", "Double Kill", spriteIndex: 0, sortingWeight: 10, medalDifficultyID: 1, medalTypeID: 1, personalScore: 10));
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task GetAsync_NoSuchMatch_ReturnsNull()
    {
        var result = await _service.GetAsync(Guid.NewGuid());

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetAsync_UnlinkedMatch_ReturnsTeamsAndPlayersWithoutSeasonInfo()
    {
        await SeedMedals();
        var xbox1 = XboxUser.FromXbox(1, "Chief117");
        var xbox2 = XboxUser.FromXbox(2, "Arbiter");
        Context.XboxUsers.AddRange(xbox1, xbox2);
        var user1 = User.Seed(userName: "chief", xboxUser: xbox1, id: 1);
        Context.Users.Add(user1);
        await Context.SaveChangesAsync();

        var matchID = Guid.NewGuid();
        var match = Match.Seed(matchID, startTime: DateTime.UtcNow, duration: TimeSpan.FromMinutes(8));
        match.AddTeam(0, 10, Outcomes.Won);
        match.AddTeam(1, 5, Outcomes.Lost);
        match.AddParticipant(0, 1, new MatchParticipantStats { Score = 10, Kills = 15, Deaths = 3 }, [new MedalCount(1, 2)]);
        match.AddParticipant(1, 2, new MatchParticipantStats { Score = 5, Kills = 8, Deaths = 10 });
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();

        var result = await _service.GetAsync(matchID);

        Assert.That(result, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.MatchID, Is.EqualTo(matchID));
            Assert.That(result.SeasonMatch, Is.Null, "no series game links it to a season match");
            Assert.That(result.Teams, Has.Length.EqualTo(2));
        }
        var winningTeam = result.Teams.Single(t => t.TeamID == 0);
        var chief = winningTeam.Players.Single(p => p.Gamertag == "Chief117");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(winningTeam.SeasonTeamID, Is.Null);
            Assert.That(chief.UserID, Is.EqualTo(1), "tied to the account through its Xbox user");
            Assert.That(chief.Kills, Is.EqualTo(15));
            Assert.That(chief.Medals.Single().Name, Is.EqualTo("Double Kill"));
            Assert.That(chief.Medals.Single().Count, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task GetAsync_LinkedToASeasonMatch_IncludesSeasonMatchInfo()
    {
        var season = Season.Seed(seasonName: "S1");
        Context.Seasons.Add(season);
        var home = Team.Seed(seasonID: season.SeasonID, teamName: "Home");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: "Away");
        Context.Teams.AddRange(home, away);
        await Context.SaveChangesAsync();
        var seasonMatch = SeasonMatch.Between(season.SeasonID, home.TeamID, away.TeamID, bestOf: 3);
        Context.SeasonMatches.Add(seasonMatch);
        await Context.SaveChangesAsync();

        var matchID = Guid.NewGuid();
        var match = Match.Seed(matchID, startTime: DateTime.UtcNow, duration: TimeSpan.FromMinutes(8));
        match.AddTeam(0, 10, Outcomes.Won);
        Context.Matches.Add(match);
        seasonMatch.Game(1, halo: match);
        await Context.SaveChangesAsync();

        var result = await _service.GetAsync(matchID);

        Assert.That(result!.SeasonMatch, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SeasonMatch!.SeasonMatchID, Is.EqualTo(seasonMatch.SeasonMatchID));
            Assert.That(result.SeasonMatch.SeasonName, Is.EqualTo("S1"));
            Assert.That(result.SeasonMatch.GameNumber, Is.EqualTo(1));
            Assert.That(result.SeasonMatch.BestOf, Is.EqualTo(3));
        }
    }
}
