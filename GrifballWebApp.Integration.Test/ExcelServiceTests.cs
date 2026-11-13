using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Excel;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Quartz;
using System.Text.Json;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Infrastructure.Jobs;
using FakeSheetsHandler = GrifballWebApp.Infrastructure.Test.FakeSheetsHandler;
using FakeSheetsHttpClientFactory = GrifballWebApp.Infrastructure.Test.FakeSheetsHttpClientFactory;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ExcelServiceTests
{
    private const string CopySpreadsheet = "copy-spreadsheet";
    private const string TargetSpreadsheet = "target-spreadsheet";
    private const int ColumnsPerRow = 197; // A..GO

    private GrifballContext _context;
    private IDataPullService _dataPull;
    private FakeSheetsHandler _handler;
    private string _lColumnJson = """{"values":[]}""";
    private string _copySheetJson = """{"values":[]}""";

    private static readonly SheetInfo Target = new() { Name = "Target", SpreadsheetID = TargetSpreadsheet, SheetName = "Stats" };
    private static readonly string[] expected = new[] { "Amy200", "Zed200", "Bob200", "Amy100", "Zed100", "Bob100" };
    private static readonly int[] expectedArray = new[] { 1, 1, 0, 1, 1, 0 };
    private static readonly int[] expectedArray0 = new[] { 2, 2, 3, 2, 2, 3 };
    private static readonly string[] expectedArray1 = new[] { "Amy300", "Zed300", "Bob300" };

    [SetUp]
    public async Task Setup()
    {
        _context = await TestDatabase.NewGrifballContext();
        _dataPull = Substitute.For<IDataPullService>();
        _handler = new FakeSheetsHandler
        {
            Respond = req =>
            {
                var url = Uri.UnescapeDataString(req.RequestUri!.ToString());
                if (req.Method == HttpMethod.Post)
                    return $$"""{"spreadsheetId":"{{TargetSpreadsheet}}"}""";
                if (url.Contains(CopySpreadsheet))
                    return _copySheetJson;
                return _lColumnJson;
            },
        };
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private static IConfiguration Config(Dictionary<string, string?>? extra = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["GoogleSheets:CopySpreadsheetID"] = CopySpreadsheet,
            ["GoogleSheets:CopySheetNameRange"] = "Matches!A:Z",
        };
        foreach (var kv in extra ?? new())
            values[kv.Key] = kv.Value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    // The internal seam: a SheetsService on the fake handler, no key file or credential
    private ExcelService CreateService(IConfiguration? config = null) =>
        new(_context, _dataPull, config ?? Config(), FakeSheetsHttpClientFactory.SheetsServiceOn(_handler));

    private async Task SeedMedals()
    {
        _context.MedalDifficulties.Add(MedalDifficulty.Imported(1, "Normal"));
        _context.MedalTypes.Add(MedalType.Imported(1, "Spree"));
        _context.Medals.Add(Medal.Imported(10, "Killing Spree", "d", spriteIndex: 0, sortingWeight: 0, medalDifficultyID: 1, medalTypeID: 1, personalScore: 0));
        _context.Medals.Add(Medal.Imported(11, "Double Kill", "d", spriteIndex: 0, sortingWeight: 0, medalDifficultyID: 1, medalTypeID: 1, personalScore: 0));
        await _context.SaveChangesAsync();
    }

    private static void Participant(Match match, int teamID, long xuid, string gamertag, int kills, int rank, params MedalCount[] medals) =>
        match.AddParticipant(teamID, xuid, new MatchParticipantStats
        {
            Kills = kills,
            MeleeKills = kills,
            Rank = rank,
            TimePlayed = TimeSpan.FromSeconds(600),
            FirstJoinedTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        }, medals, XboxUser.FromXbox(xuid, gamertag));

    /// <summary>Two-team match: team 1 won (Zed, Amy), team 0 lost (Bob).</summary>
    private async Task SeedMatch(Guid matchID, DateTime start, long xuidOffset)
    {
        var match = Match.Seed(matchID, startTime: start, duration: TimeSpan.FromMinutes(10));
        match.AddTeam(0, 0, Outcomes.Lost);
        match.AddTeam(1, 0, Outcomes.Won);
        Participant(match, 0, xuidOffset + 2, $"Bob{xuidOffset}", 1, 3);
        Participant(match, 1, xuidOffset + 3, $"Zed{xuidOffset}", 4, 2);
        Participant(match, 1, xuidOffset + 1, $"Amy{xuidOffset}", 5, 1, new MedalCount(10, 3), new MedalCount(11, 2));
        _context.Matches.Add(match);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private (string Range, JsonElement[][] Rows, string InputOption) SingleBatchUpdate()
    {
        var post = _handler.Requests.Single(r => r.Method == HttpMethod.Post);
        Assert.That(post.Url, Does.Contain($"spreadsheets/{TargetSpreadsheet}/values:batchUpdate"));
        using var doc = JsonDocument.Parse(post.Body);
        var root = doc.RootElement;
        var data = root.GetProperty("data").EnumerateArray().Single();
        var rows = data.GetProperty("values").EnumerateArray()
            .Select(r => r.EnumerateArray().Select(c => c.Clone()).ToArray()).ToArray();
        return (data.GetProperty("range").GetString()!, rows, root.GetProperty("valueInputOption").GetString()!);
    }

    [Test]
    public void Constructor_MissingKey_Throws()
    {
        var ex = Assert.Throws<Exception>(() => new ExcelService(_context, _dataPull, Config()));
        Assert.That(ex!.Message, Is.EqualTo("Missing GoogleSheets:Key"));
    }

    [Test]
    public void GetDefaultInfo_Configured_ReturnsSheets()
    {
        var service = CreateService(Config(new()
        {
            ["GoogleSheets:Sheets:0:Name"] = "Main",
            ["GoogleSheets:Sheets:0:SpreadsheetID"] = "sheet-1",
            ["GoogleSheets:Sheets:0:SheetName"] = "Tab1",
            ["GoogleSheets:Sheets:1:Name"] = "Other",
            ["GoogleSheets:Sheets:1:SpreadsheetID"] = "sheet-2",
            ["GoogleSheets:Sheets:1:SheetName"] = "Tab2",
        }));

        var info = service.GetDefaultInfo();

        Assert.That(info.Select(x => (x.Name, x.SpreadsheetID, x.SheetName)),
            Is.EqualTo(new[] { ("Main", "sheet-1", "Tab1"), ("Other", "sheet-2", "Tab2") }));
    }

    [Test]
    public void GetDefaultInfo_EmptySection_ReturnsPlaceholder()
    {
        var service = CreateService(Config(new() { ["GoogleSheets:Sheets"] = "" }));

        var info = service.GetDefaultInfo();

        Assert.That(info, Has.Length.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(info[0].Name, Is.EqualTo("Enter your spreadsheetID and sheet name below"));
            Assert.That(info[0].SpreadsheetID, Is.Empty);
            Assert.That(info[0].SheetName, Is.Empty);
        }
    }

    [Test]
    public void GetDefaultInfo_MissingSection_Throws()
    {
        var service = CreateService();
        Assert.Throws<InvalidOperationException>(() => service.GetDefaultInfo());
    }

    [Test]
    public async Task ExportAll_PullsEveryGuidAndWritesRowsFromA2()
    {
        await SeedMedals();
        var early = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var late = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var notInDb = Guid.Parse("33333333-3333-3333-3333-333333333333");
        // Pasted in upper case (as some tools print GUIDs): still a match ID, pulled like the rest
        var upperCase = Guid.Parse("44444444-abcd-4ef0-abcd-0123456789ab");
        await SeedMatch(late, new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc), 100);
        await SeedMatch(early, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), 200);
        _copySheetJson = $$"""
        {"values":[
          ["header aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa (skipped)"],
          ["https://halowaypoint.com/halo-infinite/players/x/matches/{{late}}", "no guid here"],
          ["{{early}} and {{notInDb}}"],
          ["UPPERCASE {{upperCase.ToString().ToUpperInvariant()}} is pulled too"]
        ]}
        """;
        var service = CreateService();

        await service.ExportAll(Target);

        Received.InOrder(() =>
        {
            _ = _dataPull.GetAndSaveMatch(late);
            _ = _dataPull.GetAndSaveMatch(early);
            _ = _dataPull.GetAndSaveMatch(notInDb);
            _ = _dataPull.GetAndSaveMatch(upperCase);
        });
        await _dataPull.ReceivedWithAnyArgs(4).GetAndSaveMatch(default);

        var get = _handler.Requests.Single(r => r.Method == HttpMethod.Get);
        Assert.That(get.Url, Does.Contain($"spreadsheets/{CopySpreadsheet}/values/Matches!A:Z"));

        var (range, rows, inputOption) = SingleBatchUpdate();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(inputOption, Is.EqualTo("USER_ENTERED"));
            Assert.That(range, Is.EqualTo("Stats!A2:GO7"));
            Assert.That(rows, Has.Length.EqualTo(6));
            Assert.That(rows.All(r => r.Length == ColumnsPerRow), Is.True);
            // Earliest match first, winners first, then by gamertag
            Assert.That(rows.Select(r => r[11].GetString()), Is.EqualTo(expected));
            Assert.That(rows.Select(r => r[1].GetInt32()), Is.EqualTo(expectedArray), "team id");
            Assert.That(rows.Select(r => r[2].GetInt32()), Is.EqualTo(expectedArray0), "outcome");
            Assert.That(rows[0][0].GetString(), Is.Empty);
            Assert.That(rows[0][6].GetString(), Is.Empty, "null LastLeaveTime is sent as empty string");
            Assert.That(rows[0][10].GetDouble(), Is.EqualTo(600), "time played in seconds");
            Assert.That(rows[0][28].GetInt32(), Is.EqualTo(5), "kills");
            // Medal counts via Exts.Count: Killing Spree and Double Kill for Amy, zero otherwise
            Assert.That(rows[0][30].GetInt32(), Is.EqualTo(3));
            Assert.That(rows[0][85].GetInt32(), Is.EqualTo(2));
            Assert.That(rows[0][31].GetInt32(), Is.Zero);
            Assert.That(rows[1][30].GetInt32(), Is.Zero);
            Assert.That(rows[0][181].GetInt32(), Is.EqualTo(5), "melee kills");
            Assert.That(rows[0][196].GetInt32(), Is.EqualTo(1), "rank is the last column");
            Assert.That(rows[2][196].GetInt32(), Is.EqualTo(3));
        }
    }

    [Test]
    public async Task ExportAll_NoGuidsInSheet_DoesNotWrite()
    {
        _copySheetJson = """{"values":[["header"],["nothing"],["here"]]}""";
        var service = CreateService();

        await service.ExportAll(Target);

        await _dataPull.DidNotReceiveWithAnyArgs().GetAndSaveMatch(default);
        Assert.That(_handler.Requests.Any(r => r.Method == HttpMethod.Post), Is.False);
    }

    [Test]
    public async Task ExportAll_MissingCopySpreadsheetConfig_Throws()
    {
        var service = CreateService(Config(new() { ["GoogleSheets:CopySpreadsheetID"] = null }));

        var ex = await Assert.ThrowsAsync<Exception>(() => service.ExportAll(Target));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo("Missing GoogleSheets:CopySpreadsheetID"));
            Assert.That(_handler.Requests, Is.Empty);
        }
    }

    [Test]
    public async Task ExportAll_MissingCopySheetRangeConfig_Throws()
    {
        var service = CreateService(Config(new() { ["GoogleSheets:CopySheetNameRange"] = null }));

        var ex = await Assert.ThrowsAsync<Exception>(() => service.ExportAll(Target));

        Assert.That(ex!.Message, Is.EqualTo("Missing GoogleSheets:CopySheetNameRange"));
    }

    [Test]
    public async Task AppendMatch_WritesAfterLastFilledRowOfColumnL()
    {
        await SeedMedals();
        var matchID = Guid.NewGuid();
        await SeedMatch(matchID, new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), 300);
        _lColumnJson = """{"values":[["Gamertag"],["a"],["b"],["c"],["d"]]}""";
        var service = CreateService();

        await service.AppendMatch(Target, matchID);

        await _dataPull.Received(1).GetAndSaveMatch(matchID);
        var get = _handler.Requests.Single(r => r.Method == HttpMethod.Get);
        Assert.That(get.Url, Does.Contain($"spreadsheets/{TargetSpreadsheet}/values/Stats!L:L"));
        var (range, rows, _) = SingleBatchUpdate();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(range, Is.EqualTo("Stats!A6:GO8"));
            Assert.That(rows.Select(r => r[11].GetString()), Is.EqualTo(expectedArray1));
        }
    }

    [Test]
    public async Task AppendMatch_UnknownMatch_DoesNotWrite()
    {
        _lColumnJson = """{"values":[["Gamertag"]]}""";
        var service = CreateService();
        var matchID = Guid.NewGuid();

        await service.AppendMatch(Target, matchID);

        await _dataPull.Received(1).GetAndSaveMatch(matchID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_handler.Requests.Count(r => r.Method == HttpMethod.Get), Is.EqualTo(1));
            Assert.That(_handler.Requests.Any(r => r.Method == HttpMethod.Post), Is.False);
        }
    }

    // ---------------- ExcelController ----------------

    [Test]
    public void Controller_DefaultSheetInfo_ReturnsOkWithInfo()
    {
        var controller = new ExcelController(CreateService(Config(new() { ["GoogleSheets:Sheets"] = "" })));

        var result = controller.DefaultSheetInfo();

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.TypeOf<SheetInfo[]>());
    }

    [Test]
    public async Task Controller_ExportAll_QueuesTheExport_OnceAtATime()
    {
        _copySheetJson = """{"values":[["header"]]}""";
        var controller = new ExcelController(CreateService());
        await using var jobs = await InMemoryJobs.Create(TimeProvider.System, null);

        var idle = await controller.ExportAllStatus(jobs.OnDemand, CancellationToken.None);
        var queued = (await controller.ExportAll(Target, jobs.OnDemand, CancellationToken.None)).Accepted();
        var again = (await controller.ExportAll(Target, jobs.OnDemand, CancellationToken.None)).Accepted();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((idle.State, queued.State, again.State), Is.EqualTo((OnDemandJobState.Idle, OnDemandJobState.Queued, OnDemandJobState.Queued)));
            Assert.That((await controller.ExportAllStatus(jobs.OnDemand, CancellationToken.None)).State, Is.EqualTo(OnDemandJobState.Queued));
            Assert.That(await jobs.Scheduler.GetTriggersOfJob(OnDemandJobs.JobKeyOf(typeof(SheetExportJob))), Has.Count.EqualTo(1));
            Assert.That(_handler.Requests, Is.Empty, "Nothing written until the worker runs it");
        }
    }

    [Test]
    public async Task TheExportJob_ExportsToItsSheet()
    {
        _copySheetJson = """{"values":[["header"]]}""";
        var context = Substitute.For<Quartz.IJobExecutionContext>();

        await new SheetExportJob(CreateService()).Execute(context, Target);

        Assert.That(_handler.Requests, Has.Count.EqualTo(1), "Read the copy sheet; no match in it, nothing written");
    }

    [Test]
    public async Task Controller_AppendMatch_ReturnsOk()
    {
        var controller = new ExcelController(CreateService());
        var matchID = Guid.NewGuid();

        var result = await controller.AppendMatch(matchID, Target);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        await _dataPull.Received(1).GetAndSaveMatch(matchID);
    }
}
