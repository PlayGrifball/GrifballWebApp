using System.Security.Cryptography;
using System.Text.Json;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.Excel;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using GrifballWebApp.Application.HaloInfinite;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// ExcelService: ExportToExcel's row building (every medal column, outcomes, team ordering, null handling) and the
/// Google Sheets calls around it, over a fake HttpMessageHandler standing in for the real Sheets API (never the
/// network), and a real (temp-file SQLite) GrifballContext for the matches it reads. The service is built through its
/// internal constructor with a real SheetsService on that handler (<see cref="FakeSheetsHttpClientFactory.SheetsServiceOn"/>,
/// shared with GrifballWebApp.Integration.Test): no key file or credential. One test builds it through the public
/// constructor from a generated service-account key file.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ExcelServiceExportUnitTests : SqliteContextFixture
{
    private const string CopySpreadsheet = "copy-spreadsheet";
    private const string TargetSpreadsheet = "target-spreadsheet";
    private const int ColumnsPerRow = 197; // A..GO

    private IDataPullService _dataPull = null!;
    private FakeSheetsHandler _handler = null!;
    private string _lColumnJson = """{"values":[]}""";
    private string _copySheetJson = """{"values":[]}""";

    private static readonly SheetInfo Target = new() { Name = "Target", SpreadsheetID = TargetSpreadsheet, SheetName = "Stats" };
    private static readonly string[] expected = ["Amy200", "Zed200", "Bob200", "Amy100", "Zed100", "Bob100"];
    private static readonly int[] expectedArray = [1, 1, 0, 1, 1, 0];
    private static readonly int[] expectedArray0 = [2, 2, 3, 2, 2, 3];
    private static readonly string[] expectedArray1 = ["Amy300", "Zed300", "Bob300"];
    private static readonly string[] MainOnly = ["Main"];

    [SetUp]
    public void Setup()
    {
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
        new(Context, _dataPull, config ?? Config(), FakeSheetsHttpClientFactory.SheetsServiceOn(_handler));

    private async Task SeedMedals()
    {
        Context.MedalDifficulties.Add(MedalDifficulty.Imported(1, "Normal"));
        Context.MedalTypes.Add(MedalType.Imported(1, "Spree"));
        Context.Medals.Add(Medal.Imported(10, "Killing Spree", "d", spriteIndex: 0, sortingWeight: 0, medalDifficultyID: 1, medalTypeID: 1, personalScore: 0));
        Context.Medals.Add(Medal.Imported(11, "Double Kill", "d", spriteIndex: 0, sortingWeight: 0, medalDifficultyID: 1, medalTypeID: 1, personalScore: 0));
        await Context.SaveChangesAsync();
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
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
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
        var ex = Assert.Throws<Exception>(() => new ExcelService(Context, _dataPull, Config()));
        Assert.That(ex!.Message, Is.EqualTo("Missing GoogleSheets:Key"));
    }

    /// <summary>
    /// The public constructor (the one dependency injection uses) reads the configured service-account key file and
    /// builds its own SheetsService, without calling Google. The only test that needs a key: generated here, once.
    /// </summary>
    [Test]
    public async Task Constructor_ServiceAccountKeyFile_BuildsTheService()
    {
        var keyFile = Path.Combine(Path.GetTempPath(), $"grif-excel-key-{Guid.NewGuid():N}.json");
        try
        {
            using (var rsa = RSA.Create(2048))
            {
                var key = new Dictionary<string, string>
                {
                    ["type"] = "service_account",
                    ["project_id"] = "test-project",
                    ["private_key_id"] = "abc123",
                    ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
                    ["client_email"] = "test@test-project.iam.gserviceaccount.com",
                    ["client_id"] = "1234567890",
                    ["token_uri"] = "https://oauth2.googleapis.com/token",
                };
                await File.WriteAllTextAsync(keyFile, JsonSerializer.Serialize(key));
            }

            var service = new ExcelService(Context, _dataPull, Config(new()
            {
                ["GoogleSheets:Key"] = keyFile,
                ["GoogleSheets:Sheets:0:Name"] = "Main",
                ["GoogleSheets:Sheets:0:SpreadsheetID"] = "sheet-1",
                ["GoogleSheets:Sheets:0:SheetName"] = "Tab1",
            }));

            Assert.That(service.GetDefaultInfo().Select(x => x.Name), Is.EqualTo(MainOnly));
        }
        finally
        {
            File.Delete(keyFile);
        }
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

    /// <summary>The same game twice in the sheet (a lower-case Waypoint URL, and its ID pasted in upper case): pulled once.</summary>
    [Test]
    public async Task ExportAll_SameGameInTwoCases_PullsItOnce()
    {
        var game = Guid.Parse("55555555-abcd-4ef0-abcd-0123456789ab");
        _copySheetJson = $$"""
        {"values":[
          ["header"],
          ["https://halowaypoint.com/halo-infinite/players/x/matches/{{game}}"],
          ["{{game.ToString().ToUpperInvariant()}}"]
        ]}
        """;
        var service = CreateService();

        await service.ExportAll(Target);

        Assert.That(_dataPull.ReceivedCalls().Count(), Is.EqualTo(1), "One pull, not one per copy");
        await _dataPull.Received(1).GetAndSaveMatch(game);
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

    [Test]
    public async Task TheExportJob_ExportsTheCopySheetsMatches_ToItsInput()
    {
        var id = Guid.Parse("55555555-5555-5555-5555-555555555555");
        _copySheetJson = $$"""{"values":[["header"],["{{id}}"]]}""";
        var run = Substitute.For<Quartz.IJobExecutionContext>();

        await new SheetExportJob(CreateService()).Execute(run, Target);

        run.Received(1).ReportProgress(0, "Exporting to Stats");
        await _dataPull.Received(1).GetAndSaveMatch(id);
    }
}
