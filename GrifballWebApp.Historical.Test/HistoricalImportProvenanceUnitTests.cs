using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Historical.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using GrifballWebApp.Application.HaloInfinite;
using static GrifballWebApp.Historical.Test.HistoricalImportUnitTestData;

namespace GrifballWebApp.Historical.Test;

/// <summary>
/// Where the import says each row came from (<see cref="HistoricalProvenance"/>): the cells that made it, kept; the ones the
/// latest run read it from, replaced each run; nothing of a season rolled back. Against the fake SQLite context.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
internal sealed class HistoricalImportProvenanceUnitTests : ApplicationDbContextTests
{
    private const string Workbook = "winter-league-2020";
    private const string SeasonKey = "winter-league-2020/Stats";
    private static readonly string[] AwardCells = ["D2"];
    private static readonly string[] TitleCells = ["A3:E3"];
    private static readonly string[] MatchCells = ["A12:F13", "A9:D9", "C4"];
    private static readonly string[] GameCells = ["A12:F13"];
    private static readonly string[] LineCells = ["A12:F12", "A13:F13"];
    private HistoricalImportService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new HistoricalImportService(Context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }),
            NullLogger<HistoricalImportService>.Instance, new GamertagMatching(Context), TimeProvider.System, Substitute.For<IUserMergeService>());
    }

    private static ArchiveOrigin Cells(string cells, string value, string sheet = "Stats") => new(Workbook, sheet, cells, value);

    private static ArchivePlayer Row(string gamertag, string team, string cells)
    {
        var player = Player(gamertag, team: team);
        player.Origin = Cells(cells, $"{gamertag} | {team}");
        return player;
    }

    private static ArchiveSeason Season(params ArchivePlayer[] players)
    {
        var season = Entry("Winter League 2020", "reach", "AGLA", false, players);
        season.Origin = Cells("A1:B1", "Gamertag | Team");
        return season;
    }

    private static HistoricalArchiveInput Archive(ArchiveSeason season, int snapshotID = 7) => new()
    {
        Seasons = new UnifiedArchive { Seasons = [season] },
        SnapshotIDs = new() { [Workbook] = snapshotID },
    };

    private async Task<List<HistoricalProvenance>> Rows(string entity)
        => await Context.HistoricalProvenance.AsNoTracking().Where(p => p.Entity == entity).OrderBy(p => p.HistoricalProvenanceID).ToListAsync();

    private async Task<int> UserOf(string gamertag) => await Context.Users.Where(u => u.DisplayName == gamertag).Select(u => u.Id).SingleAsync();

    [Test]
    public async Task Import_AStatRow_SaysWhichCellsMadeThePlayerTheirLineTheirTeamAndTheSeason()
    {
        var report = await _service.Run(Archive(Season(Row("Church", "Red Team", "A5:B5"))), new HistoricalImportRunOptions { JobID = 42 });

        var church = await UserOf("Church");
        var user = (await Rows("User")).Single();
        var line = (await Rows(nameof(Domain.League.PlayerSeasonStat))).Single();
        var team = (await Rows(nameof(Domain.League.Team))).Single();
        var roster = (await Rows(nameof(Domain.League.TeamPlayer))).Single();
        var season = (await Rows(nameof(Domain.League.Season))).Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That((user.EntityKey, user.Kind, user.SourceID, user.Sheet, user.Cells, user.Value),
                Is.EqualTo((church.ToString(System.Globalization.CultureInfo.InvariantCulture), HistoricalProvenanceKind.Created, Workbook, "Stats", "A5:B5", "Church | Red Team")));
            Assert.That(user.Detail, Does.StartWith("Placeholder for Church: no account"), "why the import made a placeholder");
            Assert.That((user.HistoricalImportJobID, user.HistoricalSourceSnapshotID), Is.EqualTo(((int?)42, (int?)7)), "the run's job and the snapshot it read");
            Assert.That((line.Kind, line.Cells), Is.EqualTo((HistoricalProvenanceKind.Created, "A5:B5")));
            Assert.That((team.Kind, team.Cells, team.Detail), Is.EqualTo((HistoricalProvenanceKind.Created, "A5:B5", "Church's stat row names Red Team")));
            Assert.That((roster.Kind, roster.Cells), Is.EqualTo((HistoricalProvenanceKind.Created, "A5:B5")));
            Assert.That((season.Kind, season.Cells), Is.EqualTo((HistoricalProvenanceKind.Created, "A1:B1")), "a season is its tab's header");
            Assert.That(new[] { user, line, team, roster, season }.Select(p => p.RunID).Distinct().Count(), Is.EqualTo(1), "one run");
        }
    }

    [Test]
    public async Task Import_Again_ReplacesTheReadRows_AndKeepsTheCellsThatMadeIt()
    {
        await _service.Import(Archive(Season(Row("Church", "Red Team", "A5:B5"))));
        Context.ChangeTracker.Clear();

        // The row moved down a line on the sheet
        await _service.Import(Archive(Season(Row("Church", "Red Team", "A6:B6")), snapshotID: 8));
        Context.ChangeTracker.Clear();
        await _service.Import(Archive(Season(Row("Church", "Red Team", "A7:B7")), snapshotID: 9));

        var lines = await Rows(nameof(Domain.League.PlayerSeasonStat));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(lines.Select(l => (l.Kind, l.Cells, l.HistoricalSourceSnapshotID)), Is.EqualTo(new[]
            {
                (HistoricalProvenanceKind.Created, "A5:B5", (int?)7),
                (HistoricalProvenanceKind.Read, "A7:B7", (int?)9),
            }), "the first run's cells stay; the latest run's replace the run before's");
            Assert.That(await Rows("User"), Has.Count.EqualTo(1), "a placeholder is made once, and its cells stay");
        }
    }

    [Test]
    public async Task Run_ADryRun_LeavesNoProvenance()
    {
        var report = await _service.Run(Archive(Season(Row("Church", "Red Team", "A5:B5"))), new HistoricalImportRunOptions { DryRun = true });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.SeasonsCreated, Is.EqualTo(1), "it would make the season");
            Assert.That(await Context.HistoricalProvenance.CountAsync(), Is.Zero, "rolled back with the season");
        }
    }

    [Test]
    public async Task Import_AnArchiveThatSaysNothingOfItsCells_KeysOnItsWorkbookAndTab()
    {
        // An uploaded archive: no origins, no snapshots
        await _service.Import(Input(Entry("Winter League 2020", "reach", "AGLA", false, Player("Church", team: "Red Team"))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await Rows(nameof(Domain.League.Season))).Select(p => (p.SourceID, p.Sheet, p.Cells, p.HistoricalSourceSnapshotID)),
                Is.EqualTo(new[] { (Workbook, (string?)"Stats", (string?)null, (int?)null) }));
            Assert.That(await Rows("User"), Is.Empty, "no cell said where the player came from");
        }
    }

    [Test]
    public async Task Import_EachKindOfRecord_SaysWhereItCameFrom()
    {
        var season = Season(Row("Church", "Red Team", "A5:B5"), Row("Tex", "Blue Team", "A6:B6"));
        season.Awards.Add(new ArchiveAward { Name = "MVP", Winner = "Church", Origin = Cells("D2", "Church", "Awards") });
        var input = Archive(season);
        var archive = new HistoricalArchiveInput
        {
            Seasons = input.Seasons,
            SnapshotIDs = input.SnapshotIDs,
            Championships = new ChampionsArchive
            {
                Champions = [new ArchiveChampionship { Season = "Winter League 2020", Tier = "Open", Team = "Red Team", Roster = ["Church"], Origin = Cells("A3:E3", "Winter League 2020 | Red Team", "Champions") }],
            },
            ScheduledMatches = [new ArchiveScheduledMatchInput
            {
                SeasonSourceId = SeasonKey, SourceKey = "s@1", Home = "Red Team", Away = "Blue Team",
                PlayedAt = new DateTime(2020, 1, 5, 1, 0, 0, DateTimeKind.Utc), Origin = Cells("C4", "Red Team vs. Blue Team", "Schedule"),
            }],
            Forfeits = [new ArchiveForfeitInput
            {
                SeasonSourceId = SeasonKey, SourceKey = "f#9", Winner = "Blue Team", Loser = "Red Team", Origin = Cells("A9:D9", "Blue Team | Red Team | FF", "Results"),
            }],
            RecordedGames = [new RecordedGameInput
            {
                SeasonSourceId = SeasonKey, SourceKey = "g#12", GameVersion = "reach", PlayedAt = new DateTime(2020, 1, 12, 1, 0, 0, DateTimeKind.Utc),
                Origin = Cells("A12:F13", "game", "Games"),
                Teams = [new RecordedTeamInput { Side = 1, Outcome = Outcomes.Won }, new RecordedTeamInput { Side = 2, Outcome = Outcomes.Lost }],
                Players =
                [
                    new RecordedPlayerInput { Gamertag = "Church", Side = 1, Kills = 3, Origin = Cells("A12:F12", "Church", "Games") },
                    new RecordedPlayerInput { Gamertag = "Tex", Side = 2, Kills = 1, Origin = Cells("A13:F13", "Tex", "Games") },
                ],
            }],
        };

        var report = await _service.Import(archive);

        var cells = await Context.HistoricalProvenance.AsNoTracking().Select(p => new { p.Entity, p.Sheet, p.Cells }).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(cells.Where(c => c.Entity == nameof(Domain.League.SeasonAward)).Select(c => c.Cells), Is.EqualTo(AwardCells));
            Assert.That(cells.Where(c => c.Entity == nameof(Domain.League.PlayerChampionship)).Select(c => c.Cells), Is.EqualTo(TitleCells));
            Assert.That(cells.Where(c => c.Entity == nameof(Domain.League.SeasonMatch)).Select(c => c.Cells).Order(),
                Is.EqualTo(MatchCells), "the scheduled match, the forfeit and the recorded game's series");
            Assert.That(cells.Where(c => c.Entity == nameof(RecordedGame)).Select(c => c.Cells), Is.EqualTo(GameCells));
            Assert.That(cells.Where(c => c.Entity == nameof(RecordedGameTeam)), Has.Exactly(2).Items, "each side");
            Assert.That(cells.Where(c => c.Entity == nameof(RecordedGameParticipant)).Select(c => c.Cells).Order(), Is.EqualTo(LineCells),
                "each player's line, their own row");
            Assert.That(cells.Where(c => c.Entity == nameof(Domain.League.Team) && c.Sheet == "Champions"), Has.Exactly(1).Items, "Red Team's title names it too");
        }
    }

    [Test]
    public async Task Import_APlayersListsDiscordID_SaysWhichRowLinkedIt()
    {
        await _service.Import(Archive(Season(Row("Church", "Red Team", "A5:B5"))));
        Context.ChangeTracker.Clear();

        await _service.Import(new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive(),
            Players = [new ArchivePlayerInput { Gamertag = "Church", DiscordId = "123456789012345678", Region = "East", Origin = new ArchiveOrigin("ll-master", "Players", "A2:E2", "Church | 123456789012345678") }],
        });

        var church = (await UserOf("Church")).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var changes = await Context.HistoricalProvenance.AsNoTracking().Where(p => p.Entity == "User" && p.EntityKey == church && p.Kind == HistoricalProvenanceKind.Changed)
            .Select(p => new { p.SourceID, p.Cells, p.Detail }).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(changes.Select(c => (c.SourceID, c.Cells)), Is.EquivalentTo(new[] { ("ll-master", (string?)"A2:E2"), ("ll-master", "A2:E2") }), "the Discord link and the region");
            Assert.That(changes.Select(c => c.Detail), Has.Some.StartsWith("Discord ID 123456789012345678 linked").And.Some.StartsWith("Region set"));
        }
    }
}
