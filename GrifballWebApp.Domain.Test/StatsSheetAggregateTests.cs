using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// The stats sheets are small aggregates (#335): a season's (<see cref="PlayerSeasonStat"/>) and a matchmaking season's
/// (<see cref="MatchmakingPlayerStat"/>) rows begin from their sheet, a re-run of the import restates them from the same
/// slot's row only, and a merge moves only the source's rows.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class StatsSheetAggregateTests
{
    private static StatSheetTotals Sheet(string gamertag = "Shadow", int goals = 3) => new()
    {
        Gamertag = gamertag,
        GamesPlayed = 10,
        Wins = 7,
        Losses = 3,
        WinPercent = 0.7m,
        Goals = goals,
        Kills = 40,
        Deaths = 20,
        Assists = 5,
        KdRatio = 2m,
        KdSpread = 20,
        CarrierKills = 4,
        AverageLifeSeconds = 31.5m,
        TotalSecondsPlayed = 6000,
        Sprees = 2,
        MultiKills = 3,
        Exterminations = 1,
        Betrayals = 1,
        HammerKills = 30,
        SwordKills = 6,
        BallKills = 4,
        Punches = 2,
        Score = 900,
        DamageDealt = 5000,
        DamageTaken = 4000,
        ExtendedMetricsJson = """{"Assassinations":2}""",
    };

    [Test]
    public void ASeasonsRow_FromItsSheet_KeepsTheSheetsValues()
    {
        var row = PlayerSeasonStat.FromSheet(4, 9, 6, Sheet(), teamID: 12, stintIndex: 2, isPlayoffs: true, isCombined: true, tradeNote: "From Hens", roleNote: "Sub");

        Assert.Multiple(() =>
        {
            Assert.That((row.SeasonID, row.UserID, row.GameVersionID, row.TeamID, row.StintIndex, row.IsPlayoffs, row.IsCombined, row.TradeNote, row.RoleNote),
                Is.EqualTo((4, 9, 6, (int?)12, 2, true, true, "From Hens", "Sub")));
            Assert.That(row.Totals, Is.EqualTo(Sheet()), "Every total, as the sheet gave it");
        });
    }

    [Test]
    public void ASeasonsRow_IsTheFirstStintOfTheRegularSeason_UnlessTheSheetSaysOtherwise()
    {
        var row = PlayerSeasonStat.FromSheet(4, 9, 6, Sheet());

        Assert.That((row.TeamID, row.StintIndex, row.IsPlayoffs, row.IsCombined), Is.EqualTo(((int?)null, 1, false, false)));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ASeasonsRow_RefusesAStintBelowOne(int stint)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerSeasonStat.FromSheet(4, 9, 6, Sheet(), stintIndex: stint));
    }

    [TestCase(null)]
    [TestCase(" ")]
    public void ARow_RefusesASheetWithoutAGamertag(string? gamertag)
    {
        Assert.Multiple(() =>
        {
            Assert.Throws(Is.InstanceOf<ArgumentException>(), () => PlayerSeasonStat.FromSheet(4, 9, 6, Sheet() with { Gamertag = gamertag! }));
            Assert.Throws(Is.InstanceOf<ArgumentException>(), () => MatchmakingPlayerStat.FromSheet(4, 9, 6, Sheet() with { Gamertag = gamertag! }));
            Assert.Throws<ArgumentNullException>(() => MatchmakingPlayerStat.FromSheet(4, 9, 6, null!));
        });
    }

    [Test]
    public void ASeasonsRow_IsRestated_ByTheSameStintsRow()
    {
        var row = PlayerSeasonStat.FromSheet(4, 9, 6, Sheet(), teamID: 12, stintIndex: 2, isPlayoffs: true, tradeNote: "From Hens");
        var sheet = PlayerSeasonStat.FromSheet(4, 9, 7, Sheet(gamertag: "Shadow S16", goals: 8), teamID: null, stintIndex: 2, isPlayoffs: true, isCombined: true, roleNote: "Sub");

        Assert.That(row.IsSameStintAs(sheet), Is.True);
        row.Restate(sheet);

        Assert.Multiple(() =>
        {
            Assert.That((row.GameVersionID, row.TeamID, row.IsCombined, row.TradeNote, row.RoleNote), Is.EqualTo((7, (int?)null, true, (string?)null, "Sub")));
            Assert.That(row.Totals, Is.EqualTo(Sheet(gamertag: "Shadow S16", goals: 8)));
        });
    }

    [Test]
    public void ASeasonsRow_RefusesARestate_ByAnotherSlotsRow()
    {
        var row = PlayerSeasonStat.FromSheet(4, 9, 6, Sheet());
        var others = new[]
        {
            PlayerSeasonStat.FromSheet(5, 9, 6, Sheet(goals: 8)),
            PlayerSeasonStat.FromSheet(4, 10, 6, Sheet(goals: 8)),
            PlayerSeasonStat.FromSheet(4, 9, 6, Sheet(goals: 8), stintIndex: 2),
            PlayerSeasonStat.FromSheet(4, 9, 6, Sheet(goals: 8), isPlayoffs: true),
        };

        Assert.Multiple(() =>
        {
            foreach (var other in others)
                Assert.Throws<InvalidOperationException>(() => row.Restate(other));
            Assert.Throws<InvalidOperationException>(() => row.Restate(null!));
            Assert.That(row.Goals, Is.EqualTo(3), "Nothing changed");
        });
    }

    [Test]
    public void AMatchmakingRow_IsRestated_ByTheSamePlayersRow_Only()
    {
        var row = MatchmakingPlayerStat.FromSheet(3, 9, 1, Sheet());

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => row.Restate(MatchmakingPlayerStat.FromSheet(4, 9, 1, Sheet(goals: 8))));
            Assert.Throws<InvalidOperationException>(() => row.Restate(MatchmakingPlayerStat.FromSheet(3, 10, 1, Sheet(goals: 8))));
            Assert.That(row.IsSameSlotAs(null!), Is.False);
        });
        row.Restate(MatchmakingPlayerStat.FromSheet(3, 9, 2, Sheet(goals: 8)));

        Assert.That((row.MatchmakingSeasonID, row.UserID, row.GameVersionID, row.Goals), Is.EqualTo((3, 9, 2, 8)));
    }

    [Test]
    public void AMerge_MovesOnlyTheSourcesRow()
    {
        var season = PlayerSeasonStat.FromSheet(4, 9, 6, Sheet());
        var matchmaking = MatchmakingPlayerStat.FromSheet(3, 9, 1, Sheet());

        Assert.Multiple(() =>
        {
            Assert.That(season.ReassignUser(10, 11), Is.False, "Someone else's row");
            Assert.That(season.UserID, Is.EqualTo(9));
            Assert.That(season.ReassignUser(9, 11), Is.True);
            Assert.That(season.UserID, Is.EqualTo(11));
            Assert.That(matchmaking.ReassignUser(9, 11), Is.True);
            Assert.That(matchmaking.UserID, Is.EqualTo(11));
        });
    }

    [Test]
    public void AStintsTeam_CanGo_TheRowStays()
    {
        var row = PlayerSeasonStat.FromSheet(4, 9, 6, Sheet(), teamID: 12);

        row.RemoveTeam();

        Assert.That((row.TeamID, row.Team, row.Goals), Is.EqualTo(((int?)null, (Team?)null, 3)));
    }

    [Test]
    public void ASeed_OfEntitiesNotSavedYet_KeepsTheNavigations()
    {
        var user = User.Seed(userName: "seeded");
        var late = MatchmakingSeason.Seed("Late League");
        var team = Team.Seed(teamName: "Reds", teamID: 5);

        var row = PlayerSeasonStat.Seed(Sheet(), gameVersionID: 1, user: user, team: team, stintIndex: 0);
        var lateRow = MatchmakingPlayerStat.Seed(Sheet(), gameVersionID: 1, matchmakingSeason: late, user: user);

        Assert.Multiple(() =>
        {
            Assert.That((row.User, row.Team, row.TeamID, row.StintIndex), Is.EqualTo((user, team, (int?)5, 0)), "A seed may be what an older model let be");
            Assert.That((lateRow.User, lateRow.MatchmakingSeason), Is.EqualTo((user, late)));
        });
    }
}
