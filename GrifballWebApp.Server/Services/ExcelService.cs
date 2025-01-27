using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Auth.OAuth2;
using System.Text.RegularExpressions;

namespace GrifballWebApp.Server.Services;

public class ExcelService
{
    private readonly GrifballContext _context;
    private readonly DataPullService _dataPullService;
    private readonly SheetsService _sheetsService;
    private readonly IConfiguration _configuration;
    public ExcelService(GrifballContext context, DataPullService dataPullService, IConfiguration configuration)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        _context = context;
        _dataPullService = dataPullService;
        _configuration = configuration;

        var credential = GoogleCredential.FromFile(configuration.GetValue<string>("GoogleSheets:Key") ?? throw new Exception("Missing GoogleSheets:Key"))
            .CreateScoped(SheetsService.Scope.Spreadsheets);
        _sheetsService = new SheetsService(new Google.Apis.Services.BaseClientService.Initializer()
        {
            HttpClientInitializer = credential
        });
    }

    private static string GetExcelRange(IList<IList<object>> data, int startRow)
    {
        if (data == null || data.Count == 0 || data[0].Count == 0)
            return string.Empty;

        int endColumnNumber = data[0].Count - 1;
        int endRowNumber = data.Count - 1 + startRow;

        string endColumn = GetColumnName(endColumnNumber);

        return $"A{startRow}:{endColumn}{endRowNumber}";
    }

    private static string GetColumnName(int columnNumber)
    {
        string columnName = string.Empty;
        while (columnNumber >= 0)
        {
            columnName = (char)('A' + (columnNumber % 26)) + columnName;
            columnNumber = columnNumber / 26 - 1;
        }
        return columnName;
    }


    private async Task<List<Guid>> FetchDataToExport()
    {
        var guids = new List<Guid>();

        var spreadsheetID = _configuration.GetValue<string>("GoogleSheets:CopySpreadsheetID")
            ?? throw new Exception("Missing GoogleSheets:CopySpreadsheetID");
        var sheetName = _configuration.GetValue<string>("GoogleSheets:CopySheetNameRange")
            ?? throw new Exception("Missing GoogleSheets:CopySheetNameRange");
        var response = await _sheetsService.Spreadsheets.Values.Get(spreadsheetID, sheetName).ExecuteAsync();

        var rows = response.Values.Skip(1).SelectMany(x => x).Cast<string>().ToList();

        string pattern = @"[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}";
        foreach(var str in rows)
        {
            MatchCollection matches = Regex.Matches(str, pattern);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                guids.Add(Guid.Parse(match.Value));
            }
        }

        foreach(var guid in guids)
        {
            await _dataPullService.GetAndSaveMatch(guid);
        }

        return guids;
    }


    public async Task ExportToSheets()
    {
        var guids = await FetchDataToExport();
        var spreadsheetID = _configuration.GetValue<string>("GoogleSheets:SpreadsheetID")
            ?? throw new Exception("Missing GoogleSheets:SpreadsheetID");
        var sheetName = _configuration.GetValue<string>("GoogleSheets:SheetName")
            ?? throw new Exception("Missing GoogleSheets:SheetName");

        // Example tinker data
        //var testRange = "A2:GO12";
        //var response = await _sheetsService.Spreadsheets.Values.Get(spreadsheetID, sheetName + "!" + testRange).ExecuteAsync();
        //response.Values[0][11] = "Test";
        //await UpdateData(spreadsheetID, sheetName, response.Values);

        var matches = _context.Matches
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.MatchTeams)
                .ThenInclude(x => x.MatchParticipants)
                    .ThenInclude(x => x.MedalEarned)
                        .ThenInclude(x => x.Medal)
            .Include(x => x.MatchTeams)
                .ThenInclude(x => x.MatchParticipants)
                    .ThenInclude(x => x.XboxUser)
            .OrderBy(x => x.StartTime)
            .Where(x => guids.Contains(x.MatchID))
            .ToArray();

        var startRow = 2;
        var row = startRow;
        var data = new List<IList<object>>();
        foreach (var match in matches.OrderBy(x => x.StartTime))
        {
            foreach (var team in match.MatchTeams.OrderBy(x => x.Outcome)) // Winning team first
            {
                foreach (var s in team.MatchParticipants.OrderBy(x => x.XboxUser.Gamertag))
                {
                    var rowData = new List<object>();
                    rowData.AddRange(
                        "", // Bot att
                        s.MatchTeam.TeamID, // LastTeamID, not currently tracked
                        s.MatchTeam.Outcome, //Make sure int
                        //s.MatchTeam.Outcome == Outcomes.Won ? 1 : 0, // Wins
                        //s.MatchTeam.Outcome == Outcomes.Lost ? 1 : 0, // Losses
                        "", // Confirmed participation
                        s.FirstJoinedTime,
                        s.JoinedInProgress,
                        // Send empty string instead of null because
                        // null will not overwrite existing value
                        s.LastLeaveTime is null ? "" : s.LastLeaveTime,
                        s.LeftInProgress,
                        s.PresentAtBeginning,
                        s.PresentAtCompletion,
                        s.TimePlayed.TotalSeconds,
                        s.XboxUser.Gamertag,
                        s.Accuracy,
                        s.Assists,
                        s.AverageLife.TotalSeconds,
                        s.Betrayals,
                        s.CalloutAssists,
                        s.DamageDealt,
                        s.DamageTaken,
                        s.Deaths,
                        s.DamageDealt,
                        s.DamageTaken,
                        0, // Driver assists
                        0, // EMP assists
                        0, // Grenade kills
                        0, // Headshot kills
                        0, // Hijacks
                        s.Kda,
                        s.Kills,
                        s.MaxKillingSpree,
                        //s.Score, // Goals
                        //s.Kills - s.PowerWeaponKills, // Ball punches = Kills - Power Weapon Kills
                        //s.Kills - s.Deaths,
                        //(decimal)s.Kills / (decimal)Math.Max(s.Deaths, 1), // Prevent division by 0
                        //s.MatchTeam.Match.Duration.TotalMinutes,
                        s.MedalEarned.Count("Killing Spree"),
                        s.MedalEarned.Count("Killing Frenzy"),
                        s.MedalEarned.Count("Running Riot"),
                        s.MedalEarned.Count("Rampage"),
                        s.MedalEarned.Count("Killjoy"), // TODO: confirm spelling
                        s.MedalEarned.Count("Nightmare"), // TODO: confirm spelling
                        s.MedalEarned.Count("Boogeyman"), // TODO: confirm spelling
                        s.MedalEarned.Count("Grim Reaper"), // TODO: confirm spelling
                        s.MedalEarned.Count("Demon"), // TODO: confirm spelling
                        s.MedalEarned.Count("Death Cabbie"), // TODO: confirm spelling
                        s.MedalEarned.Count("Driving Spree"), // TODO: confirm spelling
                        s.MedalEarned.Count("Immortal Chauffeur"), // TODO: confirm spelling
                        s.MedalEarned.Count("Perfection"), // TODO: confirm spelling
                        s.MedalEarned.Count("Flawless Victory"), // TODO: confirm spelling
                        s.MedalEarned.Count("Steaktacular"), // TODO: confirm spelling
                        s.MedalEarned.Count("Stopped Short"), // TODO: confirm spelling
                        s.MedalEarned.Count("Flag Joust"), // TODO: confirm spelling
                        s.MedalEarned.Count("Goal Line Stand"), // TODO: confirm spelling
                        s.MedalEarned.Count("Necromancer"), // TODO: confirm spelling
                        s.MedalEarned.Count("Immortal"), // TODO: confirm spelling
                        s.MedalEarned.Count("Lone Wolf"), // TODO: confirm spelling
                        s.MedalEarned.Count("Duelist"), // TODO: confirm spelling
                        s.MedalEarned.Count("Ace"), // TODO: confirm spelling
                        s.MedalEarned.Count("Extermination"), // TODO: confirm spelling
                        s.MedalEarned.Count("Fumble"), // TODO: confirm spelling
                        s.MedalEarned.Count("Straight Balling"), // TODO: confirm spelling
                        s.MedalEarned.Count("Always Rotating"), // TODO: confirm spelling
                        s.MedalEarned.Count("Clock Stop"), // TODO: confirm spelling
                        s.MedalEarned.Count("All That Juice"), // TODO: confirm spelling
                        s.MedalEarned.Count("Great Journey"), // TODO: confirm spelling
                        s.MedalEarned.Count("Power Outage"), // TODO: confirm spelling
                        s.MedalEarned.Count("Sole Survivor"), // TODO: confirm spelling
                        s.MedalEarned.Count("Culling"), // TODO: confirm spelling
                        s.MedalEarned.Count("Blight"), // TODO: confirm spelling
                        s.MedalEarned.Count("Zombie Slayer"), // TODO: confirm spelling
                        s.MedalEarned.Count("Purge"), // TODO: confirm spelling
                        s.MedalEarned.Count("Disease"), // TODO: confirm spelling
                        s.MedalEarned.Count("Undead Hunter"), // TODO: confirm spelling
                        s.MedalEarned.Count("Untainted"), // TODO: confirm spelling
                        s.MedalEarned.Count("Cleansing"), // TODO: confirm spelling
                        s.MedalEarned.Count("Plague"), // TODO: confirm spelling
                        s.MedalEarned.Count("Hell's Janitor"), // TODO: confirm spelling
                        s.MedalEarned.Count("The Sickness"), // TODO: confirm spelling
                        s.MedalEarned.Count("Purification"), // TODO: confirm spelling
                        s.MedalEarned.Count("Pestilence"), // TODO: confirm spelling
                        s.MedalEarned.Count("Divine Intervention"), // TODO: confirm spelling
                        s.MedalEarned.Count("Scourge"), // TODO: confirm spelling
                        s.MedalEarned.Count("Apocalypse"), // TODO: confirm spelling
                        s.MedalEarned.Count("Clear Reception"), // TODO: confirm spelling
                        s.MedalEarned.Count("Hang Up"), // TODO: confirm spelling
                        s.MedalEarned.Count("Call Blocked"), // TODO: confirm spelling
                        s.MedalEarned.Count("Secure Line"), // TODO: confirm spelling
                        s.MedalEarned.Count("Signal Block"), // TODO: confirm spelling
                        s.MedalEarned.Count("Monopoly"), // TODO: confirm spelling
                        s.MedalEarned.Count("Hill Guardian"), // TODO: confirm spelling
                        s.MedalEarned.Count("Double Kill"),
                        s.MedalEarned.Count("Triple Kill"),
                        s.MedalEarned.Count("Overkill"),
                        s.MedalEarned.Count("Killtacular"),
                        s.MedalEarned.Count("Killtrocity"),
                        s.MedalEarned.Count("Killamanjaro"),
                        s.MedalEarned.Count("Killtastrophe"),
                        s.MedalEarned.Count("Killpocalypse"),
                        s.MedalEarned.Count("Killionaire"),
                        s.MedalEarned.Count("Spotter"), // TODO: confirm spelling
                        s.MedalEarned.Count("Treasure Hunter"), // TODO: confirm spelling
                        s.MedalEarned.Count("Saboteur"), // TODO: confirm spelling
                        s.MedalEarned.Count("Wingman"), // TODO: confirm spelling
                        s.MedalEarned.Count("Wheelman"), // TODO: confirm spelling
                        s.MedalEarned.Count("Gunner"), // TODO: confirm spelling
                        s.MedalEarned.Count("Driver"), // TODO: confirm spelling
                        s.MedalEarned.Count("Pilot"), // TODO: confirm spelling
                        s.MedalEarned.Count("Tanker"), // TODO: confirm spelling
                        s.MedalEarned.Count("Rifleman"), // TODO: confirm spelling
                        s.MedalEarned.Count("Bomber"), // TODO: confirm spelling
                        s.MedalEarned.Count("Grenadier"), // TODO: confirm spelling
                        s.MedalEarned.Count("Boxer"), // TODO: confirm spelling
                        s.MedalEarned.Count("Warrior"), // TODO: confirm spelling
                        s.MedalEarned.Count("Gunslinger"), // TODO: confirm spelling
                        s.MedalEarned.Count("Scattergunner"), // TODO: confirm spelling
                        s.MedalEarned.Count("Sharpshooter"), // TODO: confirm spelling
                        s.MedalEarned.Count("Marksman"), // TODO: confirm spelling
                        s.MedalEarned.Count("Heavy"), // TODO: confirm spelling
                        s.MedalEarned.Count("Bodyguard"), // TODO: confirm spelling
                        s.MedalEarned.Count("Breacher"), // TODO: confirm spelling
                        s.MedalEarned.Count("Back Smack"), // TODO: confirm spelling
                        s.MedalEarned.Count("Nuclear Football"), // TODO: confirm spelling
                        s.MedalEarned.Count("Boom Block"), // TODO: confirm spelling
                        s.MedalEarned.Count("Bulltrue"), // TODO: confirm spelling
                        s.MedalEarned.Count("Cluster Luck"), // TODO: confirm spelling
                        s.MedalEarned.Count("Dogfight"), // TODO: confirm spelling
                        s.MedalEarned.Count("Harpoon"), // TODO: confirm spelling
                        s.MedalEarned.Count("Mind the Gap"), // TODO: confirm spelling
                        s.MedalEarned.Count("Ninja"), // TODO: confirm spelling
                        s.MedalEarned.Count("Odin's Raven"), // TODO: confirm spelling
                        s.MedalEarned.Count("Pancake"), // TODO: confirm spelling
                        s.MedalEarned.Count("Quigley"), // TODO: confirm spelling
                        s.MedalEarned.Count("Remote Detonation"), // TODO: confirm spelling
                        s.MedalEarned.Count("Return to Sender"), // TODO: confirm spelling
                        s.MedalEarned.Count("Rideshare"), // TODO: confirm spelling
                        s.MedalEarned.Count("Skyjack"), // TODO: confirm spelling
                        s.MedalEarned.Count("Stick"), // TODO: confirm spelling
                        s.MedalEarned.Count("Tag & Bag"), // TODO: confirm spelling
                        s.MedalEarned.Count("Whiplash"), // TODO: confirm spelling
                        s.MedalEarned.Count("Kong"), // TODO: confirm spelling
                        s.MedalEarned.Count("Autopilot Engaged"), // TODO: confirm spelling
                        s.MedalEarned.Count("Sneak King"), // TODO: confirm spelling
                        s.MedalEarned.Count("Windshield Wiper"), // TODO: confirm spelling
                        s.MedalEarned.Count("Reversal"), // TODO: confirm spelling
                        s.MedalEarned.Count("Hail Mary"), // TODO: confirm spelling
                        s.MedalEarned.Count("Nade Shot"), // TODO: confirm spelling
                        s.MedalEarned.Count("Snipe"), // TODO: confirm spelling
                        s.MedalEarned.Count("Perfect"), // TODO: confirm spelling
                        s.MedalEarned.Count("Bank Shot"), // TODO: confirm spelling
                        s.MedalEarned.Count("Fire & Forget"), // TODO: confirm spelling
                        s.MedalEarned.Count("Ballista"), // TODO: confirm spelling
                        s.MedalEarned.Count("Pull"), // TODO: confirm spelling
                        s.MedalEarned.Count("No Scope"), // TODO: confirm spelling
                        s.MedalEarned.Count("Achilles Spine"), // TODO: confirm spelling
                        s.MedalEarned.Count("Grand Slam"), // TODO: confirm spelling
                        s.MedalEarned.Count("Guardian Angel"), // TODO: confirm spelling
                        s.MedalEarned.Count("Interlinked"), // TODO: confirm spelling
                        s.MedalEarned.Count("Death Race"), // TODO: confirm spelling
                        s.MedalEarned.Count("Chain Reaction"), // TODO: confirm spelling
                        s.MedalEarned.Count("Splatter"), // TODO: confirm spelling
                        s.MedalEarned.Count("Counter-snipe"), // TODO: confirm spelling
                        s.MedalEarned.Count("360"), // TODO: confirm spelling
                        s.MedalEarned.Count("Combat Evolved"), // TODO: confirm spelling
                        s.MedalEarned.Count("Deadly Catch"), // TODO: confirm spelling
                        s.MedalEarned.Count("Driveby"), // TODO: confirm spelling
                        s.MedalEarned.Count("Fastball"), // TODO: confirm spelling
                        s.MedalEarned.Count("Flyin' High"), // TODO: confirm spelling
                        s.MedalEarned.Count("From the Grave"), // TODO: confirm spelling
                        s.MedalEarned.Count("From the Void"), // TODO: confirm spelling
                        s.MedalEarned.Count("Grapple-jack"), // TODO: confirm spelling
                        s.MedalEarned.Count("Hold This"), // TODO: confirm spelling
                        s.MedalEarned.Count("Last Shot"), // TODO: confirm spelling
                        s.MedalEarned.Count("Lawnmower"), // TODO: confirm spelling
                        s.MedalEarned.Count("Mount Up"), // TODO: confirm spelling
                        s.MedalEarned.Count("Off the Rack"), // TODO: confirm spelling
                        s.MedalEarned.Count("Quick Draw"), // TODO: confirm spelling
                        s.MedalEarned.Count("Party's Over"), // TODO: confirm spelling
                        s.MedalEarned.Count("Pineapple Express"), // TODO: confirm spelling
                        s.MedalEarned.Count("Ramming Speed"), // TODO: confirm spelling
                        s.MedalEarned.Count("Reclaimer"), // TODO: confirm spelling
                        s.MedalEarned.Count("Shot Caller"), // TODO: confirm spelling
                        s.MedalEarned.Count("Yard Sale"), // TODO: confirm spelling
                        s.MedalEarned.Count("Special Delivery"), // TODO: confirm spelling
                        s.MedalEarned.Count("Street Sweeper"), // TODO: confirm spelling
                        s.MedalEarned.Count("Mounted & Loaded"), // TODO: confirm spelling
                        s.MedalEarned.Count("Blind Fire"), // TODO: confirm spelling
                        s.MeleeKills,
                        0, // Objectives completed
                        s.PersonalScore,
                        s.PowerWeaponKills,
                        s.RoundsLost,
                        s.RoundsTied,
                        s.RoundsWon,
                        s.Score,
                        s.ShotsFired,
                        s.ShotsHit,
                        s.Deaths + 1, // Spawns, wat?
                        s.Suicides,
                        0, // Vehicle Destroys
                        s.TeamID,
                        1, // Player type
                        0 // Rank TODO is this needed?
                        //s.MatchTeam.Match.StartTime.ToString(),
                        //s.MatchTeam.Match.MatchID
                        );
                    row++;
                    data.Add(rowData);
                }   
            }
        }
        if (data.Any() is false)
            return;

        var response = await UpdateData(spreadsheetID, sheetName, data, startRow);
    }

    private async Task<BatchUpdateValuesResponse> UpdateData(string spreadsheetID, string sheet, IList<IList<object>> data, int startRow = 2)
    {
        var range = GetExcelRange(data, startRow);
        var full = $"{sheet}!{range}";
        var dataValueRange = new ValueRange()
        {
            Range = full,
            Values = data,
        };

        List<ValueRange> updateData = new List<ValueRange>();
        BatchUpdateValuesRequest requestBody = new BatchUpdateValuesRequest();
        requestBody.ValueInputOption = "USER_ENTERED";
        requestBody.Data = updateData;
        updateData.Add(dataValueRange);
        var response = await _sheetsService.Spreadsheets.Values.BatchUpdate(requestBody, spreadsheetID).ExecuteAsync();
        return response;
    }

    public void CreateExcelPerMatchOld()
    {
        FileInfo newFile = new FileInfo("AllMatchStats.xlsx");
        if (newFile.Exists)
        {
            newFile.Delete();
            newFile = new FileInfo("AllMatchStats.xlsx");
        }

        using var package = new ExcelPackage(newFile);
        var worksheet = package.Workbook.Worksheets.Add("AllMatchStats");

        var matches = _context.Matches
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.MatchTeams)
                .ThenInclude(x => x.MatchParticipants)
                    .ThenInclude(x => x.MedalEarned)
                        .ThenInclude(x => x.Medal)
            .Include(x => x.MatchTeams)
                .ThenInclude(x => x.MatchParticipants)
                    .ThenInclude(x => x.XboxUser)
            .OrderBy(x => x.StartTime)
            .Where(x => x.MatchLink.SeasonMatch.SeasonID == 4)
            .ToArray();

        var column = 1;
        worksheet.Cells[1, column++].Value = "Player";
        worksheet.Cells[1, column++].Value = "Wins";
        worksheet.Cells[1, column++].Value = "Losses";
        worksheet.Cells[1, column++].Value = "Score";
        worksheet.Cells[1, column++].Value = "Damage Dealt";
        worksheet.Cells[1, column++].Value = "Damage Taken";
        worksheet.Cells[1, column++].Value = "Goals";
        worksheet.Cells[1, column++].Value = "Ball Punches";
        worksheet.Cells[1, column++].Value = "Kills";
        worksheet.Cells[1, column++].Value = "Deaths";
        worksheet.Cells[1, column++].Value = "Spread";
        worksheet.Cells[1, column++].Value = "KD Ratio";
        worksheet.Cells[1, column++].Value = "Assists";
        worksheet.Cells[1, column++].Value = "Game Time";
        worksheet.Cells[1, column++].Value = "Killing Sprees";
        worksheet.Cells[1, column++].Value = "Killing Frenzies";
        worksheet.Cells[1, column++].Value = "Running Riots";
        worksheet.Cells[1, column++].Value = "Rampages";
        worksheet.Cells[1, column++].Value = "Grand Slam";
        worksheet.Cells[1, column++].Value = "Double Kills";
        worksheet.Cells[1, column++].Value = "Triple Kills";
        worksheet.Cells[1, column++].Value = "Overkills";
        worksheet.Cells[1, column++].Value = "Killtaculars";
        worksheet.Cells[1, column++].Value = "Killtrocities";
        worksheet.Cells[1, column++].Value = "Killamanjaros";
        worksheet.Cells[1, column++].Value = "Killtastrophe";
        worksheet.Cells[1, column++].Value = "Killpocalypses";
        worksheet.Cells[1, column++].Value = "Killionaire";
        worksheet.Cells[1, column++].Value = "Exterms";
        worksheet.Cells[1, column++].Value = "Bulltrues";
        worksheet.Cells[1, column++].Value = "Ninja";
        worksheet.Cells[1, column++].Value = "Pancake";
        worksheet.Cells[1, column++].Value = "Whiplash";
        worksheet.Cells[1, column++].Value = "Killjoys";
        worksheet.Cells[1, column++].Value = "Harpoons";
        worksheet.Cells[1, column++].Value = "Start Time UTC";
        worksheet.Cells[1, column++].Value = "MatchID";

        var columnCount = 37;

        using var header = worksheet.Cells[1, 1, 1, columnCount];
        header.Style.Font.Bold = true;
        header.AutoFitColumns();

        using var firstRow = worksheet.Cells[2, 1, 2, columnCount];
        firstRow.Style.Border.Top.Style = ExcelBorderStyle.Medium;
        firstRow.Style.Border.Top.Color.SetColor(System.Drawing.Color.Black);

        package.Workbook.Properties.Title = "Grifball Stats";
        package.Workbook.Properties.Author = "Noah Surprenant";

        var row = 2;
        foreach (var match in matches)
        {
            foreach (var team in match.MatchTeams.OrderBy(x => x.Outcome)) // Winning team first
            {
                foreach (var s in team.MatchParticipants.OrderBy(x => x.XboxUser.Gamertag))
                {
                    column = 1;
                    worksheet.Cells[row, column++].Value = s.XboxUser.Gamertag;

                    if (s.XboxUser.Gamertag is "Grunt Padre" or "ASpence501" or "Sauted Smurf" or "TommyWstSide")
                    {
                        worksheet.Cells[row, column - 1].SetHyperlink(new Uri("https://www.youtube.com/watch?v=mHjDfIazM7Y"));
                    }

                    worksheet.Cells[row, column++].Value = s.MatchTeam.Outcome == Outcomes.Won ? 1 : 0; // Wins
                    worksheet.Cells[row, column++].Value = s.MatchTeam.Outcome == Outcomes.Lost ? 1 : 0; // Losses
                    worksheet.Cells[row, column++].Value = s.PersonalScore;
                    worksheet.Cells[row, column++].Value = s.DamageDealt;
                    worksheet.Cells[row, column++].Value = s.DamageTaken;
                    worksheet.Cells[row, column++].Value = s.Score; // Goals
                    worksheet.Cells[row, column++].Value = s.Kills - s.PowerWeaponKills; // Ball punches = Kills - Power Weapon Kills
                    worksheet.Cells[row, column++].Value = s.Kills;
                    worksheet.Cells[row, column++].Value = s.Deaths;
                    worksheet.Cells[row, column++].Value = s.Kills - s.Deaths;
                    worksheet.Cells[row, column++].Value = (decimal)s.Kills / (decimal)Math.Max(s.Deaths, 1); // Prevent division by 0
                    worksheet.Cells[row, column++].Value = s.Assists;
                    worksheet.Cells[row, column++].Value = s.MatchTeam.Match.Duration.TotalMinutes;
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Killing Spree");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Killing Frenzy");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Running Riot");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Rampage");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Grand Slam");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Double Kill");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Triple Kill");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Overkill");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Killtacular");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Killtrocity");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Killamanjaro");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Killtastrophe");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Killpocalypse");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Killionaire");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Extermination");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Bulltrue");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Ninja");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Pancake");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Whiplash");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Killjoy");
                    worksheet.Cells[row, column++].Value = s.MedalEarned.Count("Harpoon");
                    worksheet.Cells[row, column++].Value = s.MatchTeam.Match.StartTime.ToString();
                    worksheet.Cells[row, column++].Value = s.MatchTeam.Match.MatchID;
                    row++;
                }
            }

            // Add underline
            using var nextRow = worksheet.Cells[row, 1, row, columnCount];
            nextRow.Style.Border.Top.Style = ExcelBorderStyle.Medium;
            nextRow.Style.Border.Top.Color.SetColor(System.Drawing.Color.Black);
        }

        worksheet.Cells[1, 1, row, 1].AutoFitColumns();

        package.Save();
    }
}

public static class Exts
{
    public static int Count(this ICollection<MedalEarned> medals, string name)
    {
        var f = medals.FirstOrDefault(x => x.Medal.MedalName == name);
        var c = f?.Count;

        return c ?? 0;
    }
}
