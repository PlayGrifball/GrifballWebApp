using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.LateLeague;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using static GrifballWebApp.Historical.Test.HistoricalImportUnitTestData;
using GrifballWebApp.Historical.Import;

namespace GrifballWebApp.Historical.Test;

/// <summary>
/// A Late League season's ladder through HistoricalImportService (#508): its rows by player, a season with only a ladder,
/// a run again, a second row for one player, a player off the ladder, and the guardrail counting ladder rows.
/// </summary>
[TestFixture]
internal sealed class HistoricalImportLadderUnitTests : ApplicationDbContextTests
{
    private const string LadderKey = "late-league-aspence-main/H3 Season 2 Leaderboard";

    private HistoricalImportService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new HistoricalImportService(Context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }),
            NullLogger<HistoricalImportService>.Instance, new GamertagMatching(Context), TimeProvider.System);
    }

    private static LadderStanding Rank(string gamertag, int? position, string? league = "Silver", int? mmr = 350) => new()
    {
        Gamertag = gamertag, Position = position, Division = "4", League = league, Rank = 1, LeaguePoints = 8, Streak = -1, Mmr = mmr, Wins = 2, Losses = 1,
    };

    /// <summary>A season with only a ladder (no stats tab), as the builder makes one.</summary>
    private static ArchiveSeason Ladder(params LadderStanding[] ladder) => new()
    {
        SeasonName = "Late League Season 2 (Halo 3)",
        GameVersion = "halo3",
        League = "Late League",
        Format = "LateLeague",
        SourceWorkbook = "late-league-aspence-main",
        SourceSheet = "H3 Season 2 Leaderboard",
        SourceYear = 2019,
        Ladder = [.. ladder],
    };

    [Test]
    public async Task Import_ASeasonWithOnlyALadder_IsAMatchmakingSeasonOfItsYear_WithARowPerPlayer()
    {
        var report = await _service.Import(Input(Ladder(Rank("Regresser", 1, "Diamond", 550), Rank("  i   robot9000 ", 2))));

        var season = await Context.MatchmakingSeasons.SingleAsync();
        var rows = await Context.MatchmakingLadderRanks.OrderBy(r => r.Position).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That((report.MatchmakingSeasonsCreated, report.LadderRanksInserted, report.StatsInserted), Is.EqualTo((1, 2, 0)));
            Assert.That((season.HistoricalSourceID, season.Name, season.StartsAt.Year), Is.EqualTo((LadderKey, "Late League Season 2 (Halo 3)", 2019)));
            Assert.That(rows.Select(r => (r.Gamertag, r.Position, r.League, r.Mmr, r.MatchmakingSeasonID)), Is.EqualTo(new (string, int?, string?, int?, int)[]
            {
                ("Regresser", 1, "Diamond", 550, season.MatchmakingSeasonID), ("i robot9000", 2, "Silver", 350, season.MatchmakingSeasonID),
            }), "Gamertags cleaned as a sheet's");
            Assert.That(rows.Select(r => (r.Division, r.Rank, r.LeaguePoints, r.Streak, r.Wins, r.Losses)).Distinct(),
                Is.EqualTo(new (string?, int?, int?, int?, int?, int?)[] { ("4", 1, 8, -1, 2, 1) }));
        }
    }

    [Test]
    public async Task Import_Again_RestatesTheRows_AndASecondRowForAPlayerIsSkipped()
    {
        await _service.Import(Input(Ladder(Rank("Regresser", 1), Rank("ASpence501", 2))));
        Context.ChangeTracker.Clear();

        var again = await _service.Import(Input(Ladder(Rank("Regresser", 1, "Gold"), Rank("ASpence501", 2), Rank("aspence501", 9))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((again.LadderRanksInserted, again.LadderRanksUpdated, again.LadderRanksRemoved), Is.EqualTo((0, 2, 0)));
            Assert.That(again.Skipped, Has.Some.EqualTo($"{LadderKey}: aspence501 is a second ladder row for the same player"));
            Assert.That(await Context.MatchmakingLadderRanks.Where(r => r.Gamertag == "Regresser").Select(r => r.League).SingleAsync(), Is.EqualTo("Gold"));
            Assert.That(await Context.MatchmakingLadderRanks.CountAsync(), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Import_APlayerOffTheLadder_LosesTheRow_CountedWithTheStatRowsForTheGuardrail()
    {
        await _service.Import(Input(Ladder(Rank("Regresser", 1), Rank("ASpence501", 2))));
        Context.ChangeTracker.Clear();

        var stopped = await _service.Run(Input(Ladder(Rank("Regresser", 1))), new HistoricalImportRunOptions { Guardrail = new HistoricalGuardrailOptions { MaxRemovedFraction = 0.1m } });
        Context.ChangeTracker.Clear();
        var stoppedCount = await Context.MatchmakingLadderRanks.CountAsync();
        var report = await _service.Import(Input(Ladder(Rank("Regresser", 1))));

        var removal = report.SeasonRemovals.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stopped.Warnings, Has.Some.EqualTo($"{LadderKey}: stopped for review, it would remove 1 of 2 stat rows, 0 of 0 teams, 0 of 0 titles"));
            Assert.That(stoppedCount, Is.EqualTo(2), "Rolled back");
            Assert.That(report.LadderRanksRemoved, Is.EqualTo(1));
            Assert.That((removal.StatsBefore, removal.StatsRemoved), Is.EqualTo((2, 1)));
            Assert.That(await Context.MatchmakingLadderRanks.Select(r => r.Gamertag).ToListAsync(), Is.EqualTo(RegresserOnly));
        }
    }

    private static readonly string[] RegresserOnly = ["Regresser"];

    [Test]
    public async Task Import_ASeasonWithStatsAndALadder_KeepsBoth_OnOneMatchmakingSeason()
    {
        var season = new ArchiveSeason
        {
            SeasonName = "Late League Season 8 (Halo 3)",
            GameVersion = "halo3",
            League = "Late League",
            SourceWorkbook = "late-league-aspence-main",
            SourceSheet = "S8Halo 3 Stats",
            Players = [Player("Galvann", kills: 50)],
            Ladder = [Rank("Galvann", 1, "Placements (1/3)", 100), Rank("Ladder Only", 2)],
        };

        var report = await _service.Import(Input(season));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.MatchmakingSeasonsCreated, report.StatsInserted, report.LadderRanksInserted), Is.EqualTo((1, 1, 2)));
            Assert.That(await Context.MatchmakingLadderRanks.Select(r => r.UserID).ToListAsync(),
                Does.Contain(await Context.MatchmakingPlayerStats.Select(s => s.UserID).SingleAsync()), "One player, the same user in both");
        }
    }
}
