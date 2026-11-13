using GrifballWebApp.Application.Grades;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// GradesService against the fake IApplicationDbContext: totals, per-minute stats and letter grades from a season's
/// Halo match data. Unlike UserManagementService/SeasonService's APPLY-gap queries, GetGrades never projects more than
/// one nested collection at once (each medal total is its own scalar Sum), so the fake context computes the same
/// numbers GradesServiceTotalsTests (Integration.Test) proves against the real providers.
/// </summary>
[TestFixture]
internal sealed class GradesServiceUnitTests : ApplicationDbContextTests
{
    private static readonly string[] ExpectedGamertagOrder = { "GT1", "GT2", "GT3" };
    private GradesService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        _service = new GradesService(Context);
        await SeedMedals();
    }

    private async Task SeedMedals()
    {
        Context.MedalTypes.AddRange(
            MedalType.Imported(1, "Spree"),
            MedalType.Imported(2, "Mode"),
            MedalType.Imported(3, "Multikill"));
        Context.MedalDifficulties.AddRange(
            MedalDifficulty.Imported(1, "Normal"),
            MedalDifficulty.Imported(2, "Heroic"),
            MedalDifficulty.Imported(3, "Legendary"),
            MedalDifficulty.Imported(4, "Mythic"));
        Context.Medals.AddRange(
            M(1, "Killing Spree", 1, 1),
            M(2, "Killjoy", 1, 1),
            M(3, "Double Kill", 3, 1),
            M(4, "Triple Kill", 3, 2),
            M(5, "Overkill", 3, 4),
            M(6, "Grand Slam", 2, 1),
            M(7, "Pancake", 2, 1),
            M(8, "Whiplash", 2, 1),
            M(9, "Killtacular", 3, 3)); // multikill but difficulty 3: not counted
        await Context.SaveChangesAsync();

        static Medal M(long id, string name, int type, int difficulty)
            => Medal.Imported(id, name, name, spriteIndex: 0, sortingWeight: 0, medalDifficultyID: difficulty, medalTypeID: type, personalScore: 0);
    }

    private async Task<(Season Season, SeasonMatch SeasonMatch)> SeedSeason(string name = "Grades Season")
    {
        var season = Season.Seed(seasonName: name);
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var sm = SeasonMatch.Seed(season.SeasonID, null, null, bestOf: 5);
        Context.SeasonMatches.Add(sm);
        await Context.SaveChangesAsync();
        return (season, sm);
    }

    private sealed record P(long Xbox, int Score, int Kills, int Deaths, int PowerWeaponKills, Dictionary<long, int>? Medals = null);

    private async Task AddLinkedMatch(int? seasonMatchID, int matchNumber, TimeSpan duration, params P[] players)
    {
        foreach (var p in players)
            if (!await Context.XboxUsers.AnyAsync(x => x.XboxUserID == p.Xbox))
                Context.XboxUsers.Add(XboxUser.FromXbox(p.Xbox, $"GT{p.Xbox}"));

        var id = Guid.NewGuid();
        var match = Match.Seed(id, startTime: DateTime.UtcNow, duration: duration);
        match.AddTeam(0, 0, Outcomes.Won);
        foreach (var p in players)
            match.AddParticipant(0, p.Xbox, new MatchParticipantStats { Score = p.Score, Kills = p.Kills, Deaths = p.Deaths, PowerWeaponKills = p.PowerWeaponKills },
                (p.Medals ?? new()).Select(m => new MedalCount(m.Key, m.Value)));
        Context.Matches.Add(match);
        if (seasonMatchID is not null)
            Context.Set<SeriesGame>().Add(new SeriesGame { MatchID = id, SeasonMatchID = seasonMatchID.Value, GameNumber = matchNumber });
        await Context.SaveChangesAsync();
    }

    private async Task<int> SeedThreePlayers()
    {
        var (season, sm) = await SeedSeason();
        await AddLinkedMatch(sm.SeasonMatchID, 1, TimeSpan.FromMinutes(10),
            new P(1, Score: 6, Kills: 20, Deaths: 2, PowerWeaponKills: 2, new() { [1] = 3, [2] = 2, [3] = 3, [4] = 3, [5] = 3, [6] = 1, [7] = 3, [9] = 4 }),
            new P(2, Score: 3, Kills: 10, Deaths: 5, PowerWeaponKills: 2, new() { [1] = 1, [3] = 1, [4] = 1, [5] = 1, [7] = 1 }),
            new P(3, Score: 0, Kills: 5, Deaths: 10, PowerWeaponKills: 2));
        return season.SeasonID;
    }

    [Test]
    public async Task GetGrades_NoParticipants_ReturnsEmptyTotalsAndPerMinutes()
    {
        var (season, _) = await SeedSeason();

        var result = await _service.GetGrades(season.SeasonID, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Totals, Is.Empty);
            Assert.That(result.PerMinutes, Is.Empty);
            Assert.That(result.Letters, Is.Empty);
        }
    }

    [Test]
    public async Task GetGrades_ComputesTotals()
    {
        var seasonID = await SeedThreePlayers();

        var result = await _service.GetGrades(seasonID, CancellationToken.None);

        Assert.That(result.Totals.Select(x => x.Gamertag), Is.EqualTo(ExpectedGamertagOrder), "ordered by gamertag");
        var a = result.Totals[0];
        var b = result.Totals[1];
        var c = result.Totals[2];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(a.XboxUserID, Is.EqualTo(1));
            Assert.That(a.TotalGoals, Is.EqualTo(6));
            Assert.That(a.TotalKDSpread, Is.EqualTo(18));
            Assert.That(a.TotalPunches, Is.EqualTo(18));
            Assert.That(a.TotalSprees, Is.EqualTo(3), "Killjoy is a spree-type medal but excluded");
            Assert.That(a.TotalDoubleKills, Is.EqualTo(3));
            Assert.That(a.TotalTripleKills, Is.EqualTo(3));
            Assert.That(a.TotalMultiKills, Is.EqualTo(4), "Overkill (difficulty 4) + Grand Slam; Killtacular (difficulty 3) excluded");
            Assert.That(a.TotalXFactor, Is.EqualTo(5), "Killjoy + Pancake");
            Assert.That(a.TotalKills, Is.EqualTo(20));
            Assert.That(a.TotalGameTime, Is.EqualTo(10d));

            Assert.That(b.TotalKDSpread, Is.EqualTo(5));
            Assert.That(b.TotalPunches, Is.EqualTo(8));
            Assert.That(b.TotalSprees, Is.EqualTo(1));
            Assert.That(b.TotalMultiKills, Is.EqualTo(1));
            Assert.That(b.TotalXFactor, Is.EqualTo(1));

            Assert.That(c.TotalKDSpread, Is.EqualTo(-5));
            Assert.That(c.TotalSprees, Is.Zero);
            Assert.That(c.TotalXFactor, Is.Zero);
        }
    }

    [Test]
    public async Task GetGrades_ComputesPerMinuteStats()
    {
        var seasonID = await SeedThreePlayers();

        var result = await _service.GetGrades(seasonID, CancellationToken.None);

        var a = result.PerMinutes.Single(x => x.XboxUserID == 1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.PerMinutes, Has.Count.EqualTo(3));
            Assert.That(a.Gamertag, Is.EqualTo("GT1"));
            Assert.That(a.GoalsPM, Is.EqualTo(0.6).Within(1e-9));
            Assert.That(a.KDSpreadPM, Is.EqualTo(1.8).Within(1e-9));
            Assert.That(a.PunchesPM, Is.EqualTo(1.8).Within(1e-9));
            Assert.That(a.SpreesPM, Is.EqualTo(0.3).Within(1e-9));
            Assert.That(a.DoubleKillsPM, Is.EqualTo(0.3).Within(1e-9));
            Assert.That(a.TripleKillsPM, Is.EqualTo(0.3).Within(1e-9));
            Assert.That(a.MultiKillsPM, Is.EqualTo(0.4).Within(1e-9));
            Assert.That(a.XFactorPM, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(a.KillsPM, Is.EqualTo(2.0).Within(1e-9));
        }
    }

    [Test]
    public async Task GetGrades_AssignsLetterGradesByPercentile()
    {
        var seasonID = await SeedThreePlayers();

        var result = await _service.GetGrades(seasonID, CancellationToken.None);

        var best = result.Letters.Single(x => x.XboxUserID == 1);
        var mid = result.Letters.Single(x => x.XboxUserID == 2);
        var worst = result.Letters.Single(x => x.XboxUserID == 3);
        using (Assert.EnterMultipleScope())
        {
            foreach (var (grade, letter) in new[] { (best, "S+"), (mid, "C"), (worst, "F-") })
            {
                Assert.That(new[] { grade.Goals, grade.KDSpread, grade.Punches, grade.Sprees, grade.DoubleKills, grade.TripleKills, grade.MultiKills, grade.XFactor, grade.Kills },
                    Is.All.EqualTo(letter), grade.Gamertag);
                Assert.That(grade.GradeAvg, Is.EqualTo(letter), grade.Gamertag);
            }
            const double weights = .40 + .55 + .15 + .35 + .20 + .25 + .35 + .10 + .25;
            Assert.That(best.GradeAvgMath, Is.EqualTo(11 * weights).Within(1e-9));
            Assert.That(mid.GradeAvgMath, Is.EqualTo(6 * weights).Within(1e-9));
            Assert.That(worst.GradeAvgMath, Is.EqualTo(1 * weights).Within(1e-9));
        }
    }

    [Test]
    public async Task GetGrades_SinglePlayer_GetsTopGrade()
    {
        var (season, sm) = await SeedSeason();
        await AddLinkedMatch(sm.SeasonMatchID, 1, TimeSpan.FromMinutes(12), new P(9, 1, 1, 1, 0));

        var result = await _service.GetGrades(season.SeasonID, CancellationToken.None);

        var only = result.Letters.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(only.Goals, Is.EqualTo("S+"));
            Assert.That(only.Kills, Is.EqualTo("S+"));
            Assert.That(only.GradeAvg, Is.EqualTo("S+"));
            Assert.That(result.Totals.Single().TotalGameTime, Is.EqualTo(12d));
        }
    }

    [Test]
    public async Task GetGrades_SumsGameTimeAcrossMatches_AndIgnoresOtherSeasonsAndUnlinkedMatches()
    {
        var (season, sm) = await SeedSeason();
        var (_, otherSm) = await SeedSeason("Other");
        await AddLinkedMatch(sm.SeasonMatchID, 1, TimeSpan.FromMinutes(10), new P(1, 2, 4, 1, 0));
        await AddLinkedMatch(sm.SeasonMatchID, 2, TimeSpan.FromMinutes(15), new P(1, 3, 6, 2, 1));
        // Same player in another season and an unlinked game: must not count
        await AddLinkedMatch(otherSm.SeasonMatchID, 1, TimeSpan.FromMinutes(30), new P(1, 100, 100, 0, 0));
        await AddLinkedMatch(null, 0, TimeSpan.FromMinutes(30), new P(1, 100, 100, 0, 0), new P(2, 1, 1, 1, 1));

        var result = await _service.GetGrades(season.SeasonID, CancellationToken.None);

        var total = result.Totals.Single();
        var pm = result.PerMinutes.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(total.XboxUserID, Is.EqualTo(1));
            Assert.That(total.TotalGoals, Is.EqualTo(5));
            Assert.That(total.TotalKills, Is.EqualTo(10));
            Assert.That(total.TotalKDSpread, Is.EqualTo(7));
            Assert.That(total.TotalPunches, Is.EqualTo(9));
            Assert.That(total.TotalGameTime, Is.EqualTo(25d));
            Assert.That(pm.GoalsPM, Is.EqualTo(5 / 25d).Within(1e-9));
            Assert.That(pm.KillsPM, Is.EqualTo(10 / 25d).Within(1e-9));
        }
    }

    [Test]
    public async Task GetGrades_PlayerWithAUser_GetsUserIDForTheProfileLink()
    {
        var (season, sm) = await SeedSeason();
        var xbox = XboxUser.FromXbox(42, "Linked");
        Context.XboxUsers.Add(xbox);
        var user = User.Seed(userName: "linked", xboxUser: xbox, id: 7);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        await AddLinkedMatch(sm.SeasonMatchID, 1, TimeSpan.FromMinutes(5), new P(42, 1, 1, 1, 0));
        await AddLinkedMatch(sm.SeasonMatchID, 2, TimeSpan.FromMinutes(5), new P(99, 1, 1, 1, 0)); // no user tied to this Xbox account

        var result = await _service.GetGrades(season.SeasonID, CancellationToken.None);

        var linked = result.Letters.Single(x => x.XboxUserID == 42);
        var unlinked = result.Letters.Single(x => x.XboxUserID == 99);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(linked.UserID, Is.EqualTo(7));
            Assert.That(unlinked.UserID, Is.Null);
        }
    }
}
