using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// A Late League season through HistoricalImportService (its matchmaking part): run again (rows restated, the game's
/// matched match recorded again), a second row, the dry run and the guardrail, the rows and games it no longer has,
/// a gamertag two accounts' histories have, and the recorded games it refuses.
/// </summary>
[TestFixture]
internal sealed class HistoricalImportLateLeagueUnitTests : ApplicationDbContextTests
{
    private const string LateKey = "late-league-season-8-halo-3/Stats";

    private HistoricalImportService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new HistoricalImportService(Context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }),
            NullLogger<HistoricalImportService>.Instance, new GamertagMatching(Context), TimeProvider.System);
    }

    private static ArchivePlayer Player(string gamertag) => new() { Gamertag = gamertag, Kills = 3, Deaths = 1, GamesPlayed = 1, StintIndex = 1 };

    private static ArchiveSeason Season(params ArchivePlayer[] players) => new()
    {
        SeasonName = "Late League Season 8 (Halo 3)",
        GameVersion = "halo3",
        League = "Late League",
        SourceWorkbook = "late-league-season-8-halo-3",
        SourceSheet = "Stats",
        Players = [.. players],
    };

    private static RecordedGameInput Game(string key, (string Gamertag, int Side)[] players, string gameVersion = "halo3", string? waypointMatchId = null, int sides = 2) => new()
    {
        SeasonSourceId = LateKey,
        SourceKey = key,
        GameVersion = gameVersion,
        PlayedAt = new DateTime(2016, 5, 1, 0, 0, 0, DateTimeKind.Utc),
        WaypointMatchId = waypointMatchId,
        Teams = [.. new[] { new RecordedTeamInput { Side = 1, Outcome = Outcomes.Won }, new RecordedTeamInput { Side = 2, Outcome = Outcomes.Lost } }.Take(sides)],
        Players = [.. players.Select(p => new RecordedPlayerInput { Gamertag = p.Gamertag, Side = p.Side, Kills = 5, Deaths = 3 })],
    };

    private static HistoricalArchiveInput Input(ArchiveSeason season, List<RecordedGameInput>? games = null)
        => new() { Seasons = new UnifiedArchive { Seasons = [season] }, RecordedGames = games };

    private static HistoricalArchiveInput TwoPlayersAndAGame() => Input(Season(Player("Alpha One"), Player("Beta One")), [Game("ll#1", [("Alpha One", 1), ("Beta One", 2)])]);

    [Test]
    public async Task Import_Again_RestatesTheRows_AndRecordsTheGamesMatchAgain()
    {
        await _service.Import(TwoPlayersAndAGame());
        Context.ChangeTracker.Clear();

        var again = await _service.Import(TwoPlayersAndAGame());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(again.Errors, Is.Empty);
            Assert.That((again.MatchmakingSeasonsCreated, again.MatchmakingSeasonsUpdated), Is.EqualTo((0, 1)));
            Assert.That((again.StatsInserted, again.StatsUpdated), Is.EqualTo((0, 2)));
            Assert.That(again.RecordedGamesImported, Is.EqualTo(1));
            Assert.That(await Context.MatchedMatches.CountAsync(), Is.EqualTo(1), "The same match, recorded again");
            Assert.That(await Context.RecordedGames.CountAsync(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Import_ASecondRowForTheSamePlayer_IsSkipped()
    {
        var report = await _service.Import(Input(Season(Player("Alpha One"), Player("alpha one"))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Skipped, Has.Some.EqualTo($"{LateKey}: alpha one is a second row for the same player"));
            Assert.That(await Context.MatchmakingPlayerStats.CountAsync(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Run_ADryRun_KeepsNothing_ButReportsWhatItWouldDo()
    {
        var report = await _service.Run(TwoPlayersAndAGame(), new HistoricalImportRunOptions { DryRun = true });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.MatchmakingSeasonsCreated, Is.EqualTo(1));
            Assert.That(report.StatsInserted, Is.EqualTo(2));
            Assert.That(report.SeasonRemovals.Select(r => (r.SeasonKey, r.Stopped)), Is.EqualTo(LateKeyNotStopped));
            Assert.That(await Context.MatchmakingSeasons.CountAsync(), Is.Zero);
            Assert.That(await Context.MatchedMatches.CountAsync(), Is.Zero);
        }
    }

    private static readonly (string, bool)[] LateKeyNotStopped = [(LateKey, false)];

    [Test]
    public async Task Run_TheGuardrail_StopsASeasonThatWouldLoseTooManyRows()
    {
        await _service.Import(Input(Season(Player("Alpha One"), Player("Beta One"))));
        Context.ChangeTracker.Clear();

        var report = await _service.Run(Input(Season(Player("Alpha One"))), new HistoricalImportRunOptions { Guardrail = new HistoricalGuardrailOptions { MaxRemovedFraction = 0.1m } });

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings, Has.Some.EqualTo($"{LateKey}: stopped for review, it would remove 1 of 2 stat rows, 0 of 0 teams, 0 of 0 titles"));
            Assert.That(report.SeasonRemovals.Single().Stopped, Is.True);
            Assert.That(report.StatsUpdated, Is.Zero, "Nothing of the stopped season is counted");
            Assert.That(await Context.MatchmakingPlayerStats.CountAsync(), Is.EqualTo(2), "Rolled back");
        }
    }

    [Test]
    public async Task Import_APlayerNoLongerOnTheSheet_LosesTheRow_AndTheRemovalIsReported()
    {
        await _service.Import(Input(Season(Player("Alpha One"), Player("Beta One"))));
        Context.ChangeTracker.Clear();

        var report = await _service.Import(Input(Season(Player("Alpha One"))));

        var removal = report.SeasonRemovals.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.StatsRemoved, Is.EqualTo(1));
            Assert.That((removal.SeasonKey, removal.StatsBefore, removal.StatsRemoved, removal.Stopped), Is.EqualTo((LateKey, 2, 1, false)));
            Assert.That(await Context.MatchmakingPlayerStats.Select(s => s.Gamertag).ToListAsync(), Is.EqualTo(AlphaOneOnly));
        }
    }

    private static readonly string[] AlphaOneOnly = ["Alpha One"];

    [Test]
    public async Task Import_AGameNoLongerInTheArchive_IsRemoved_WithItsMatch()
    {
        var secondGame = Game("ll#2", [("Beta One", 1), ("Alpha One", 2)]);
        await _service.Import(Input(Season(Player("Alpha One"), Player("Beta One")), [Game("ll#1", [("Alpha One", 1), ("Beta One", 2)]), secondGame]));
        Context.ChangeTracker.Clear();

        // ll#2 stays, so the season still has recorded games (with none at all, its earlier games are left alone)
        var report = await _service.Import(Input(Season(Player("Alpha One"), Player("Beta One")), [secondGame]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.RecordedGamesRemoved, Is.EqualTo(1));
            Assert.That(await Context.RecordedGames.Select(g => g.SourceKey).ToListAsync(), Is.EqualTo(SecondGameOnly));
            Assert.That(await Context.MatchedMatches.CountAsync(), Is.EqualTo(1), "ll#1's match went with it");
        }
    }

    private static readonly string[] SecondGameOnly = ["ll#2"];

    [Test]
    public async Task Import_NoRecordedGamesForTheSeason_LeavesItsEarlierGames()
    {
        await _service.Import(TwoPlayersAndAGame());
        Context.ChangeTracker.Clear();

        var report = await _service.Import(Input(Season(Player("Alpha One"), Player("Beta One")), []));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.RecordedGamesRemoved, Is.Zero);
            Assert.That(await Context.RecordedGames.CountAsync(), Is.EqualTo(1));
            Assert.That(await Context.MatchedMatches.CountAsync(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Import_AGamertagTwoAccountsHistoriesHave_IsWarnedOf()
    {
        Context.XboxUsers.AddRange(XboxUser.FromXbox(1, "First Now"), XboxUser.FromXbox(2, "Second Now"));
        Context.Set<XboxGamertagHistory>().AddRange(new XboxGamertagHistory(1, "Shared Tag"), new XboxGamertagHistory(2, "Shared Tag"));
        await Context.SaveChangesAsync();

        var report = await _service.Import(Input(Season(Player("Shared Tag"))));

        Assert.That(report.Warnings, Has.Some.EqualTo($"{LateKey}: Shared Tag is in more than one Xbox account's gamertag history for the season; left to its placeholder"));
    }

    [Test]
    public async Task Import_RecordedGames_ItCantKeep_AreSkipped_AndAWaypointIdIsWarnedOf()
    {
        var waypointId = new Guid("ffffffff-2222-3333-4444-555555555555").ToString();
        var report = await _service.Import(Input(Season(Player("Alpha One"), Player("Beta One")),
        [
            Game("ll#1", [("Alpha One", 1), ("Beta One", 2)], gameVersion: "nope"),
            Game("ll#2", [("Alpha One", 1), ("Beta One", 2)], waypointMatchId: waypointId),
            Game("LL#2", [("Alpha One", 1), ("Beta One", 2)]),
            Game("ll#3", [("Alpha One", 1), ("Beta One", 2)], sides: 1),
            Game("ll#4", [("Alpha One", 1), ("alpha one", 1), ("Beta One", 2)]),
        ]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.Skipped, Has.Some.EqualTo("Recorded game ll#1: unknown game version 'nope'"));
            Assert.That(report.Skipped, Has.Some.EqualTo("Recorded game LL#2: listed twice"));
            Assert.That(report.Skipped, Has.Some.EqualTo("Recorded game ll#3: a matchmaking game has two sides, it has 1"));
            Assert.That(report.Skipped, Has.Some.EqualTo("Recorded game ll#4: alpha one is listed twice for one player"));
            Assert.That(report.Warnings, Has.Some.EqualTo($"Recorded game ll#2: has Waypoint match {waypointId}; a Late League game is kept as the archive records it"));
            Assert.That(report.RecordedGamesImported, Is.EqualTo(2));
            Assert.That(await Context.RecordedGames.OrderBy(g => g.SourceKey).Select(g => g.SourceKey).ToListAsync(), Is.EqualTo(KeptGames));
        }
    }

    private static readonly string[] KeptGames = ["ll#2", "ll#4"];
}
