using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Historical.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using static GrifballWebApp.Historical.Test.HistoricalImportUnitTestData;

namespace GrifballWebApp.Historical.Test;

/// <summary>
/// A season's bracket sheet (<see cref="ArchiveSeason.Bracket"/>) imported as its bracket: its slots, seeds, where each
/// side comes from and the sheet's results; kept as it is when the sheet hasn't changed, drawn again when it has; its
/// playoff games put in the slot of their teams.
/// </summary>
[TestFixture]
internal sealed class HistoricalImportBracketUnitTests : ApplicationDbContextTests
{
    private const string CupKey = "mcc-cup-2016/Stats";
    private static readonly DateTime CupPlayedAt = new(2016, 5, 1, 20, 0, 0, DateTimeKind.Utc);

    private static readonly int[] BestOfs = [3, 3, 1];
    private static readonly int[] ThreeThenOne = [3, 1];
    private static readonly int[] GamesSplitOverTwoDays = [2, 0, 0];
    private static readonly string[] OmegaIsNoTeam = [$"{CupKey}: the bracket names 1 team(s) that are none of the season's (Omega); their slots have no team there"];
    private static readonly int[] GamesInTheFirstSlot = [1, 0, 0];

    private HistoricalImportService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new HistoricalImportService(Context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }),
            NullLogger<HistoricalImportService>.Instance, new GamertagMatching(Context), TimeProvider.System, Substitute.For<IUserMergeService>());
    }

    private static ArchiveBracketSeries Series(Bracket bracket, int round, int number, string home, string away, SeasonMatchSide? winner = null,
        int? homeSeed = null, int? awaySeed = null, int? homeWins = null, int? awayWins = null, int? bestOf = null) => new()
    {
        Bracket = bracket, Round = round, Number = number, Home = home, Away = away, Winner = winner,
        HomeSeed = homeSeed, AwaySeed = awaySeed, HomeWins = homeWins, AwayWins = awayWins, BestOf = bestOf,
    };

    /// <summary>Four teams: Alpha (1) beat Delta (4) 2-0, Gamma (3) beat Beta (2) 2-1, Alpha won the final.</summary>
    private static List<ArchiveBracketSeries> FourTeams() =>
    [
        Series(Bracket.Winner, 1, 1, "Alpha", "Delta", SeasonMatchSide.Home, 1, 4, 2, 0),
        Series(Bracket.Winner, 1, 2, "Beta", "Gamma", SeasonMatchSide.Away, 2, 3, 1, 2),
        Series(Bracket.Winner, 2, 3, "Alpha", "Gamma", SeasonMatchSide.Home),
    ];

    private static ArchiveSeason Cup(List<ArchiveBracketSeries> bracket, string game = "halo3")
    {
        var season = Entry("MCC Cup 2016", game, "AGLA", false,
            Player("Alpha One", team: "Alpha"), Player("Beta One", team: "Beta"), Player("Gamma One", team: "Gamma"), Player("Delta One", team: "Delta"));
        season.Bracket = new ArchiveBracket { Series = bracket };
        return season;
    }

    private static RecordedGameInput Game(string key, string home, string away, bool isPlayoffs = true, bool isScrim = false, int hoursLater = 0) => new()
    {
        IsScrim = isScrim,
        SeasonSourceId = CupKey,
        SourceKey = key,
        GameVersion = "halo3",
        PlayedAt = CupPlayedAt.AddHours(hoursLater),
        IsPlayoffs = isPlayoffs,
        Teams = [new RecordedTeamInput { Side = 1, Outcome = Outcomes.Won }, new RecordedTeamInput { Side = 2, Outcome = Outcomes.Lost }],
        Players = [new RecordedPlayerInput { Gamertag = $"{home} One", Side = 1, Kills = 5, Deaths = 3 }, new RecordedPlayerInput { Gamertag = $"{away} One", Side = 2, Kills = 3, Deaths = 5 }],
    };

    private async Task<List<SeasonMatch>> Slots()
        => await Context.SeasonMatches.AsNoTracking().Where(m => m.BracketMatch != null)
            .Include(m => m.HomeTeam).Include(m => m.AwayTeam).Include(m => m.SeriesGames)
            .Include(m => m.BracketMatch!).ThenInclude(b => b.HomeTeamPreviousMatchBracketInfo)
            .Include(m => m.BracketMatch!).ThenInclude(b => b.AwayTeamPreviousMatchBracketInfo)
            .OrderBy(m => m.BracketMatch!.MatchNumber).ToListAsync();

    [Test]
    public async Task Import_ABracketSheet_IsTheSeasonsBracket_ItsSeedsWhereTeamsComeFromAndResults()
    {
        var report = await _service.Import(Input(Cup(FourTeams())));

        var bracket = await Slots();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.Warnings, Is.Empty);
            Assert.That(bracket.Select(m => (m.HomeTeam!.TeamName, m.AwayTeam!.TeamName)), Is.EqualTo(new[] { ("Alpha", "Delta"), ("Beta", "Gamma"), ("Alpha", "Gamma") }));
            Assert.That(bracket.Select(m => (m.BracketMatch!.HomeTeamSeedNumber, m.BracketMatch.AwayTeamSeedNumber)),
                Is.EqualTo(new (int?, int?)[] { (1, 4), (2, 3), (null, null) }));
            Assert.That((bracket[2].BracketMatch!.HomeTeamPreviousMatchBracketInfo!.MatchNumber, bracket[2].BracketMatch!.AwayTeamPreviousMatchBracketInfo!.MatchNumber),
                Is.EqualTo((1, 2)), "The final's sides come from the semi-finals");
            Assert.That(bracket.Select(m => m.WinnerTeamID), Is.EqualTo(new[] { bracket[0].HomeTeamID, bracket[1].AwayTeamID, bracket[2].HomeTeamID }));
            Assert.That(bracket.Select(m => (m.HomeTeamScore, m.AwayTeamScore)), Is.EqualTo(new (int?, int?)[] { (2, 0), (1, 2), (null, null) }));
            Assert.That(bracket.Select(m => m.BestOf), Is.EqualTo(BestOfs), "The wins' series length, at least one game");
        }
    }

    [Test]
    public async Task Import_TheSameSheetAgain_KeepsTheBracket()
    {
        await _service.Import(Input(Cup(FourTeams())));
        var before = (await Slots()).Select(m => m.SeasonMatchID).ToList();
        Context.ChangeTracker.Clear();

        var report = await _service.Import(Input(Cup(FourTeams())));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.SeasonMatchesCreated, report.SeasonMatchesRemoved), Is.EqualTo((0, 0)));
            Assert.That((await Slots()).Select(m => m.SeasonMatchID), Is.EqualTo(before));
        }
    }

    [Test]
    public async Task Import_ATeamLeavesTheBracket_RemovesTheLaterSlotsFedByItsMatches_AndDrawsTheBracketAgain()
    {
        await _service.Import(Input(Cup(FourTeams())));
        Context.ChangeTracker.Clear();
        var season = Entry("MCC Cup 2016", "halo3", "AGLA", false,
            Player("Alpha One", team: "Alpha"), Player("Beta One", team: "Beta"), Player("Gamma One", team: "Gamma"), Player("Echo One", team: "Echo"));
        season.Bracket = new ArchiveBracket
        {
            Series =
            [
                Series(Bracket.Winner, 1, 1, "Alpha", "Echo", SeasonMatchSide.Home, 1, 4, 2, 0),
                Series(Bracket.Winner, 1, 2, "Beta", "Gamma", SeasonMatchSide.Away, 2, 3, 1, 2),
                Series(Bracket.Winner, 2, 3, "Alpha", "Gamma", SeasonMatchSide.Home),
            ],
        };

        var report = await _service.Import(Input(season));

        var bracket = await Slots();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.TeamsRemoved, Is.EqualTo(1));
            Assert.That(bracket.Select(m => (m.HomeTeam!.TeamName, m.AwayTeam!.TeamName)), Is.EqualTo(new[] { ("Alpha", "Echo"), ("Beta", "Gamma"), ("Alpha", "Gamma") }));
        }
    }

    [Test]
    public async Task Import_AChangedSheet_DrawsTheBracketAgain()
    {
        await _service.Import(Input(Cup(FourTeams())));
        Context.ChangeTracker.Clear();
        var changed = FourTeams();
        changed[2] = changed[2] with { Winner = SeasonMatchSide.Away };

        var report = await _service.Import(Input(Cup(changed)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.SeasonMatchesCreated, report.SeasonMatchesRemoved), Is.EqualTo((3, 3)));
            Assert.That((await Slots())[2].Winner, Is.EqualTo(SeasonMatchSide.Away));
        }
    }

    [Test]
    public async Task Import_APlayoffGameOfASlotsTeams_GoesInThatSlot_KeepingTheSheetsResult()
    {
        var report = await _service.Import(Input(Cup(FourTeams()), [Game("cup#1", "Delta", "Alpha"), Game("cup#2", "Beta", "Gamma", isPlayoffs: false)]));

        var bracket = await Slots();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.Warnings, Is.Empty);
            Assert.That(bracket.Select(m => m.SeriesGames.Count), Is.EqualTo(GamesInTheFirstSlot), "Delta and Alpha's slot, whichever side each was");
            Assert.That((bracket[0].HomeTeamScore, bracket[0].AwayTeamScore), Is.EqualTo(((int?)2, (int?)0)), "The sheet's, not the one game's");
            Assert.That(await Context.SeasonMatches.CountAsync(m => m.BracketMatch == null), Is.EqualTo(1), "A regular-season game is no slot's");
            Assert.That(report.GamesLinked, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Import_APlayoffGameNoSlotIsOf_IsAddedAsPlayed_Warned_AndKeptByTheNextImport()
    {
        var input = Input(Cup(FourTeams()), [Game("cup#1", "Beta", "Delta")]);
        var report = await _service.Import(input);
        Context.ChangeTracker.Clear();

        var again = await _service.Import(input);

        var bracket = await Slots();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings, Has.One.Contains("a playoff series (1 game(s) from 2016-05-01) is in no slot of the bracket sheet"));
            Assert.That(bracket, Has.Count.EqualTo(4));
            Assert.That(bracket[^1].SeriesGames, Has.Count.EqualTo(1));
            Assert.That((again.SeasonMatchesCreated, again.SeasonMatchesRemoved), Is.EqualTo((0, 0)), "The sheet's slots are as it draws them");
        }
    }

    [Test]
    public async Task Import_ASheetOfANameNoTeamHas_LeavesThatSideEmpty_WarnedOnce()
    {
        var report = await _service.Import(Input(Cup([Series(Bracket.Winner, 1, 1, "Alpha", "Omega", SeasonMatchSide.Home, 1, 2), Series(Bracket.Winner, 2, 2, "Alpha", "Omega")])));

        var bracket = await Slots();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings, Is.EqualTo(new[] { $"{CupKey}: the bracket names 1 team(s) that are none of the season's (Omega); their slots have no team there" }));
            Assert.That(bracket.Select(m => m.AwayTeamID), Is.All.Null);
            Assert.That(bracket[1].BracketMatch!.AwayTeamPreviousMatchBracketInfo!.MatchNumber, Is.EqualTo(1), "Omega comes from its first slot, by its name");
        }
    }

    [Test]
    public async Task Import_ASheetOfNoTeamOrAPlaceholder_SeedsBoth_WarnsOnlyOfTheName_AndKeepsItsLength()
    {
        var report = await _service.Import(Input(Cup([Series(Bracket.Winner, 1, 1, "Omega", "Team", bestOf: 5)])));

        var slot = (await Slots()).Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings, Is.EqualTo(OmegaIsNoTeam));
            Assert.That((slot.HomeTeamID, slot.AwayTeamID, slot.BestOf), Is.EqualTo(((int?)null, (int?)null, 5)));
            Assert.That((slot.BracketMatch!.HomeTeamSeedNumber, slot.BracketMatch.AwayTeamSeedNumber), Is.EqualTo(((int?)1, (int?)2)));
        }
    }

    [Test]
    public async Task Import_APlayoffGameWithASideOfNoTeam_IsInNoSlot()
    {
        var report = await _service.Import(Input(Cup(FourTeams()), [Game("cup#1", "Alpha", "Nobody"), Game("cup#2", "Nobody", "Beta")]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings, Has.Exactly(2).Contains("is in no slot of the bracket sheet"));
            Assert.That(await Slots(), Has.Count.EqualTo(5));
        }
    }

    [Test]
    public async Task Import_ARecordedGameGoneFromASheetSlot_LeavesTheSlotAndItsSheetResult()
    {
        // The scrim keeps the season's recorded games in the archive (with none at all, earlier ones are left alone)
        var scrim = Game("cup#2", "Beta", "Gamma", isPlayoffs: false, isScrim: true);
        await _service.Import(Input(Cup(FourTeams()), [Game("cup#1", "Alpha", "Delta"), scrim]));
        Context.ChangeTracker.Clear();

        var report = await _service.Import(Input(Cup(FourTeams()), [scrim]));

        var bracket = await Slots();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.RecordedGamesRemoved, Is.EqualTo(1));
            Assert.That(bracket, Has.Count.EqualTo(3), "The slot is the sheet's: it stays, and the final from it");
            Assert.That(bracket[0].SeriesGames, Is.Empty);
            Assert.That((bracket[0].Winner, bracket[0].HomeTeamScore, bracket[0].AwayTeamScore), Is.EqualTo(((SeasonMatchSide?)SeasonMatchSide.Home, (int?)2, (int?)0)));
        }
    }

    [Test]
    public async Task Import_AWaypointGameNoLongerListed_LeavesItsSheetSlot()
    {
        var matchId = Guid.NewGuid();
        var kept = Guid.NewGuid();
        await AddMatch(Context, matchId, (1, "Alpha One", Outcomes.Won, 20, 5), (4, "Delta One", Outcomes.Lost, 10, 15));
        await AddMatch(Context, kept, (1, "Alpha One", Outcomes.Won, 20, 5), (4, "Delta One", Outcomes.Lost, 10, 15));
        var season = Cup(FourTeams(), "infinite");
        HistoricalArchiveInput Listing(params Guid[] playoffs) => new()
        {
            Seasons = new UnifiedArchive { Seasons = [season] },
            WaypointMatches = new() { [CupKey] = new WaypointSeasonMatches { Playoffs = [.. playoffs.Select(p => p.ToString())] } },
        };
        await _service.Import(Listing(matchId, kept));
        Context.ChangeTracker.Clear();
        Assert.That((await Slots())[0].SeriesGames, Has.Count.EqualTo(2), "Linked to Alpha and Delta's slot");

        var report = await _service.Import(Listing(kept));

        var bracket = await Slots();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.SeasonMatchesRemoved, Is.Zero);
            Assert.That(bracket, Has.Count.EqualTo(3));
            Assert.That(bracket[0].SeriesGames.Select(g => g.MatchID), Is.EqualTo(new Guid?[] { kept }), "Only the game no longer listed goes");
            Assert.That(bracket[0].Winner, Is.EqualTo(SeasonMatchSide.Home));
        }
    }

    [Test]
    public async Task Import_OneEveningsGamesOfTwoSlotsOfTheSameTeams_AreSplitByTheSheetsWins()
    {
        // A grand final and its sudden death: 2-1, then 1-0, four games in one evening
        var sheet = Cup(
        [
            Series(Bracket.GrandFinal, 1, 1, "Alpha", "Beta", SeasonMatchSide.Away, 1, 2, 1, 2),
            Series(Bracket.GrandFinalSuddenDeath, 2, 2, "Alpha", "Beta", SeasonMatchSide.Away, homeWins: 0, awayWins: 1),
        ]);

        await _service.Import(Input(sheet, [.. Enumerable.Range(1, 4).Select(i => Game($"cup#{i}", "Beta", "Alpha"))]));

        Assert.That((await Slots()).Select(m => m.SeriesGames.Count), Is.EqualTo(ThreeThenOne));
    }

    [Test]
    public async Task Import_ALaterSeriesOfTeamsWhoseSlotsHaveGames_GoesInTheirLastSlot()
    {
        await _service.Import(Input(Cup(FourTeams()), [Game("cup#1", "Alpha", "Delta")]));
        Context.ChangeTracker.Clear();

        // The same teams the next day: a series split over two days
        var report = await _service.Import(Input(Cup(FourTeams()), [Game("cup#1", "Alpha", "Delta"), Game("cup#2", "Delta", "Alpha", hoursLater: 24)]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings, Is.Empty);
            Assert.That((await Slots()).Select(m => m.SeriesGames.Count), Is.EqualTo(GamesSplitOverTwoDays));
        }
    }

    [Test]
    public async Task ReImporting_ABracketSheetSlotsGame_NeverFlagsTheSlot_EvenWhenItsScoreDoesNotAddUp()
    {
        // #548, third review: LinkSeries never checks a bracket-sheet slot's games against its result (the sheet's
        // result is authoritative, not its games' tally) - a re-import mustn't start checking it either, just because
        // the game is now already linked to the slot. The sheet says Alpha won 2-0; only one game is ever recorded.
        var oneSeries = new List<ArchiveBracketSeries> { Series(Bracket.Winner, 1, 1, "Alpha", "Delta", SeasonMatchSide.Home, 1, 4, homeWins: 2, awayWins: 0) };

        await _service.Import(Input(Cup(oneSeries), [Game("cup#1", "Alpha", "Delta")]));
        Context.ChangeTracker.Clear();
        await _service.Import(Input(Cup(oneSeries), [Game("cup#1", "Alpha", "Delta")]));

        Assert.That(await Context.StatFlags.AnyAsync(), Is.False);
    }

    [Test]
    public async Task Import_AStandingsRankTheSheetGaveAnother_IsNotASecondSeed()
    {
        var season = Cup([Series(Bracket.Winner, 1, 1, "Alpha", "Beta", homeSeed: 3), Series(Bracket.Winner, 1, 2, "Gamma", "Delta", homeSeed: 1, awaySeed: 2)]);
        season.Teams.Add(new ArchiveTeam { TeamName = "Beta", Rank = 3 });

        await _service.Import(Input(season));

        Assert.That((await Slots())[0].BracketMatch!.AwayTeamSeedNumber, Is.EqualTo(4), "Seed 3 is Alpha's");
    }

    [Test]
    public async Task Import_ASideWithoutASeed_TakesItsStandingsRank_ElseTheNextSeedPastAll()
    {
        var season = Cup([Series(Bracket.Winner, 1, 1, "Alpha", "Beta"), Series(Bracket.Winner, 1, 2, "Gamma", "Delta", homeSeed: 2)]);
        season.Teams.Add(new ArchiveTeam { TeamName = "Alpha", Rank = 3 });

        await _service.Import(Input(season));

        var bracket = await Slots();
        Assert.That(bracket.Select(m => (m.BracketMatch!.HomeTeamSeedNumber, m.BracketMatch.AwayTeamSeedNumber)), Is.EqualTo(new (int?, int?)[] { (3, 4), (2, 5) }));
    }

    [Test]
    public async Task Import_ASheetWithATeamPlayingItself_LeavesThatSeriesOut()
    {
        var report = await _service.Import(Input(Cup([Series(Bracket.Winner, 1, 1, "Alpha", "alpha", homeSeed: 1, awaySeed: 2), Series(Bracket.Winner, 1, 2, "Beta", "Gamma", homeSeed: 3, awaySeed: 4)])));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings, Has.One.EqualTo($"{CupKey}: the bracket has Alpha playing itself; that series is left out"));
            Assert.That(await Slots(), Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Import_ASeasonFirstRebuiltFromGames_GetsTheSheetsBracket_ItsGamesInTheirSlot()
    {
        await _service.Import(Input(Cup([]), [Game("cup#1", "Alpha", "Delta")]));
        Context.ChangeTracker.Clear();
        Assert.That(await Slots(), Has.Count.EqualTo(1), "Rebuilt from the game");

        var report = await _service.Import(Input(Cup(FourTeams()), [Game("cup#1", "Alpha", "Delta")]));

        var bracket = await Slots();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.SeasonMatchesCreated, report.SeasonMatchesRemoved), Is.EqualTo((3, 1)));
            Assert.That(bracket.Select(m => m.SeriesGames.Count), Is.EqualTo(GamesInTheFirstSlot));
        }
    }
}
