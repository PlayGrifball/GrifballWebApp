using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// A player's place on a matchmaking season's ladder is an aggregate of its own (#508): written whole from its sheet,
/// restated only by the same player's row of the same season, and moved to another user only by a merge.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class MatchmakingLadderRankAggregateTests
{
    private static readonly LadderStanding Top = new()
    {
        Gamertag = "Ladder Top", Position = 1, Division = "2", League = "Diamond", Rank = 3, LeaguePoints = 5, Streak = -2, Mmr = 550, Wins = 12, Losses = 4,
    };

    [Test]
    public void FromSheet_KeepsEveryValueOfTheSheetsRow()
    {
        var row = MatchmakingLadderRank.FromSheet(8, 41, Top);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((row.MatchmakingSeasonID, row.UserID), Is.EqualTo((8, 41)));
            Assert.That(row.Standing, Is.EqualTo(Top));
            Assert.That((row.Gamertag, row.Position, row.Division, row.League, row.Rank, row.LeaguePoints, row.Streak, row.Mmr, row.Wins, row.Losses),
                Is.EqualTo(("Ladder Top", (int?)1, "2", "Diamond", (int?)3, (int?)5, (int?)-2, (int?)550, (int?)12, (int?)4)));
        }
    }

    [Test]
    public void FromSheet_TrimsItsText_AndCutsItToTheColumns()
    {
        var row = MatchmakingLadderRank.FromSheet(8, 41, new LadderStanding
        {
            Gamertag = "  " + new string('g', 60), Division = " " + new string('d', 30), League = "  ", Position = null,
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.Gamertag, Has.Length.EqualTo(MatchmakingLadderRank.GamertagMaxLength));
            Assert.That(row.Division, Has.Length.EqualTo(MatchmakingLadderRank.DivisionMaxLength));
            Assert.That(row.League, Is.Null, "A blank league is none");
            Assert.That(row.Position, Is.Null);
        }
    }

    [TestCase(" ", null, null, null)]
    [TestCase("Player", 0, null, null)]
    [TestCase("Player", null, -1, null)]
    [TestCase("Player", null, null, -1)]
    public void FromSheet_RefusesARowWithoutAGamertag_ABadPlace_OrANegativeRecord(string gamertag, int? position, int? wins, int? losses)
        => Assert.That(() => MatchmakingLadderRank.FromSheet(8, 41, new LadderStanding { Gamertag = gamertag, Position = position, Wins = wins, Losses = losses }),
            Throws.InstanceOf<ArgumentException>());

    [Test]
    public void FromSheet_RefusesNoStanding()
        => Assert.That(() => MatchmakingLadderRank.FromSheet(8, 41, null!), Throws.ArgumentNullException);

    [Test]
    public void Restate_TakesTheSameSlotsSheetRow()
    {
        var stored = MatchmakingLadderRank.FromSheet(8, 41, Top);
        var corrected = Top with { League = "Masters", Rank = 1, Position = 2 };

        stored.Restate(MatchmakingLadderRank.FromSheet(8, 41, corrected));

        Assert.That(stored.Standing, Is.EqualTo(corrected));
    }

    [TestCase(9, 41)]
    [TestCase(8, 42)]
    public void Restate_RefusesAnotherSlotsRow(int seasonId, int userId)
    {
        var stored = MatchmakingLadderRank.FromSheet(8, 41, Top);
        Assert.That(() => stored.Restate(MatchmakingLadderRank.FromSheet(seasonId, userId, Top)), Throws.InvalidOperationException);
        Assert.That(() => stored.Restate(null!), Throws.InvalidOperationException);
    }

    [Test]
    public void ReassignUser_MovesOnlyTheMergedUsersRow()
    {
        var row = MatchmakingLadderRank.FromSheet(8, 41, Top);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.ReassignUser(99, 7), Is.False);
            Assert.That(row.UserID, Is.EqualTo(41));
            Assert.That(row.ReassignUser(41, 7), Is.True);
            Assert.That(row.UserID, Is.EqualTo(7));
        }
    }

    [Test]
    public void ClashesOnMerge_IsTheTargetsRowOfASeasonTheSourceHasOneFor()
    {
        var rows = new[]
        {
            MatchmakingLadderRank.FromSheet(8, 1, Top), MatchmakingLadderRank.FromSheet(8, 2, Top),
            MatchmakingLadderRank.FromSheet(9, 2, Top), MatchmakingLadderRank.FromSheet(10, 1, Top),
        }.AsQueryable();

        var clashes = rows.Where(MatchmakingLadderRank.ClashesOnMerge(rows, fromUserID: 1, toUserID: 2)).ToList();

        Assert.That(clashes.Select(r => (r.MatchmakingSeasonID, r.UserID)), Is.EqualTo(new[] { (8, 2) }));
    }

    [Test]
    public void Seed_TakesTheKeysOfSavedEntities()
    {
        var season = MatchmakingSeason.Seed("Late League Season 2 (Halo 3)", matchmakingSeasonID: 5);
        var user = User.Seed(id: 12);

        var row = MatchmakingLadderRank.Seed(Top, matchmakingSeason: season, user: user);

        Assert.That((row.MatchmakingSeasonID, row.UserID, row.MatchmakingSeason, row.User), Is.EqualTo((5, 12, season, user)));
    }

    [Test]
    public void Seed_OfEntitiesNotSavedYet_TakesTheKeysGiven_AndHasNoKeyOfItsOwn()
    {
        var season = MatchmakingSeason.Seed("Late League Season 2 (Halo 3)");

        var row = MatchmakingLadderRank.Seed(Top, matchmakingSeasonID: 3, userID: 4, matchmakingSeason: season);

        Assert.That((row.MatchmakingLadderRankID, row.MatchmakingSeasonID, row.UserID, row.MatchmakingSeason), Is.EqualTo((0, 3, 4, season)));
    }
}
