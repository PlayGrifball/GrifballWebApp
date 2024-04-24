using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Surprenant.Grunt.Models.HaloInfinite;

namespace GrifballWebApp.Server.Services;

public class ExcelService
{
    private readonly GrifballContext _context;
    public ExcelService(GrifballContext context)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        _context = context;
    }

    public void CreateExcelPerMatch()
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
                    worksheet.Cells[row, column++].Value = (decimal)s.Kills / (decimal)s.Deaths;
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

    public void CreateExcel()
    {
        FileInfo newFile = new FileInfo("sample1.xlsx");
        if (newFile.Exists)
        {
            newFile.Delete();
            newFile = new FileInfo("sample1.xlsx");
        }

        using var package = new ExcelPackage(newFile);
        var worksheet = package.Workbook.Worksheets.Add("Stats");

        var stats = _context.MatchParticipants
            .AsSplitQuery()
            .Include(x => x.MedalEarned)
                .ThenInclude(x => x.Medal)
            .GroupBy(x => x.XboxUserID)
            .Select(x => new
            {
                Name = x.Key,
                // Wins
                // Losses
                Score = x.Sum(x => x.Score),// Goals
                PowerWeaponKills = x.Sum(x => x.PowerWeaponKills), // Ball punches = Kills - Power Weapon Kills
                Kills = x.Sum(x => x.Kills),
                Deaths = x.Sum(x => x.Deaths),
                Spread = x.Sum(x => x.Kills) - x.Sum(x => x.Deaths),
                KDRatio = (decimal)x.Sum(x => x.Kills) / (decimal)x.Sum(x => x.Deaths),
                Assists = x.Sum(x => x.Assists),
                // Game time
                KillingSprees = x.Select(x => x.MedalEarned.Count("Killing Spree")).FirstOrDefault(),
                KillingFrenzies = x.Select(x => x.MedalEarned.Count("Killing Frenzy")).FirstOrDefault(),
                RunningRiots = x.Select(x => x.MedalEarned.Count("Running Riot")).FirstOrDefault(),
                Rampages = x.Select(x => x.MedalEarned.Count("Rampage")).FirstOrDefault(),
                GrandSlam = x.Select(x => x.MedalEarned.Count("Grand Slam")).FirstOrDefault(),
                DoubleKills = x.Select(x => x.MedalEarned.Count("Double Kill")).FirstOrDefault(),
                TripleKills = x.Select(x => x.MedalEarned.Count("Triple Kill")).FirstOrDefault(),
                Overlills = x.Select(x => x.MedalEarned.Count("Overkill")).FirstOrDefault(),
                Killtaculars = x.Select(x => x.MedalEarned.Count("Killtacular")).FirstOrDefault(),
                Killtrocities = x.Select(x => x.MedalEarned.Count("Killtrocity")).FirstOrDefault(),
                Killamanjaros = x.Select(x => x.MedalEarned.Count("Killamanjaro")).FirstOrDefault(),
                Killtastrophe = x.Select(x => x.MedalEarned.Count("Killtastrophe")).FirstOrDefault(),
                Killpocalypses = x.Select(x => x.MedalEarned.Count("Killpocalypse")).FirstOrDefault(),
                Killionaire = x.Select(x => x.MedalEarned.Count("Killionaire")).FirstOrDefault(),
                Exterm = x.Select(x => x.MedalEarned.Count("Extermination")).FirstOrDefault(),
                Bulltrues = x.Select(x => x.MedalEarned.Count("Bulltrue")).FirstOrDefault(),
                Ninja = x.Select(x => x.MedalEarned.Count("Ninja")).FirstOrDefault(),
                Pancake = x.Select(x => x.MedalEarned.Count("Pancake")).FirstOrDefault(),
                Whiplash = x.Select(x => x.MedalEarned.Count("Whiplash")).FirstOrDefault(),
                Killjoys = x.Select(x => x.MedalEarned.Count("Killjoy")).FirstOrDefault(),
                Harpoons = x.Select(x => x.MedalEarned.Count("Harpoon")).FirstOrDefault(),
            })
            .ToArray();

        var f = 1;

        worksheet.Cells[1, 1].Value = "Player";
        worksheet.Cells[1, 2].Value = "Wins";
        worksheet.Cells[1, 3].Value = "Losses";
        worksheet.Cells[1, 4].Value = "Goals";
        worksheet.Cells[1, 5].Value = "Ball Punches";
        worksheet.Cells[1, 6].Value = "Kills";
        worksheet.Cells[1, 7].Value = "Deaths";
        worksheet.Cells[1, 8].Value = "Spread";
        worksheet.Cells[1, 9].Value = "KD Ratio";
        worksheet.Cells[1, 10].Value = "Assists";
        worksheet.Cells[1, 11].Value = "Game Time";
        worksheet.Cells[1, 12].Value = "Killing Sprees";
        worksheet.Cells[1, 13].Value = "Killing Frenzies";
        worksheet.Cells[1, 14].Value = "Running Riots";
        worksheet.Cells[1, 15].Value = "Rampages";
        worksheet.Cells[1, 16].Value = "Grand Slam";
        worksheet.Cells[1, 17].Value = "Double Kills";
        worksheet.Cells[1, 18].Value = "Triple Kills";
        worksheet.Cells[1, 19].Value = "Overkills";
        worksheet.Cells[1, 20].Value = "Killtaculars";
        worksheet.Cells[1, 21].Value = "Killtrocities";
        worksheet.Cells[1, 22].Value = "Killamanjaros";
        worksheet.Cells[1, 23].Value = "Killtastrophe";
        worksheet.Cells[1, 24].Value = "Killpocalypses";
        worksheet.Cells[1, 25].Value = "Killionaire";
        worksheet.Cells[1, 26].Value = "Exterms";
        worksheet.Cells[1, 27].Value = "Bulltrues";
        worksheet.Cells[1, 28].Value = "Ninja";
        worksheet.Cells[1, 29].Value = "Pancake";
        worksheet.Cells[1, 30].Value = "Whiplash";
        worksheet.Cells[1, 31].Value = "Killjoys";
        worksheet.Cells[1, 32].Value = "Harpoons";
        worksheet.Cells[1, 33].Value = "Avg Damage Dealt";
        worksheet.Cells[1, 34].Value = "Avg Damage Taken";
        worksheet.Cells[1, 35].Value = "Avg Score";

        using var range = worksheet.Cells[1, 1, 1, 35];
        range.Style.Font.Bold = true;
        //range.Style.Fill.PatternType = ExcelFillStyle.Solid;
        //range.Style.Fill.BackgroundColor.SetColor(Color.DarkBlue);
        //range.Style.Font.Color.SetColor(Color.White);

        package.Workbook.Properties.Title = "Grifball Stats";
        package.Workbook.Properties.Author = "Noah Surprenant";
        //package.Workbook.Properties.Comments = "";

        var row = 2;
        foreach (var s in stats)
        {
            var gamertag = _context.XboxUsers.Where(x => x.XboxUserID == s.Name).Select(x => x.Gamertag).FirstOrDefault() ?? "Missing GT";

            var wins = _context.MatchTeams
                .AsSplitQuery()
                .Where(x => x.MatchParticipants.Any(x => x.XboxUserID == s.Name))
                .Where(x => x.Outcome == Outcomes.Won)
                .Count();

            var losses = _context.MatchTeams
                .AsSplitQuery()
                .Where(x => x.MatchParticipants.Any(x => x.XboxUserID == s.Name))
                .Where(x => x.Outcome == Outcomes.Lost)
                .Count();

            var durations = _context.MatchTeams
                .AsSplitQuery()
                .Where(x => x.MatchParticipants.Any(x => x.XboxUserID == s.Name))
                .Select(x => x.Match)
                .Select(x => x.Duration)
                .ToArray();

            var timePlayed = durations.Sum(x => x.TotalMinutes);

            var damageDealt = _context.MatchParticipants
                .AsSplitQuery()
                .Where(x => x.XboxUserID == s.Name)
                .Select(x => x.DamageDealt)
                .ToArray();

            var avgDamageDealt = damageDealt.Sum() / damageDealt.Count();

            var damageTaken = _context.MatchParticipants
                .AsSplitQuery()
                .Where(x => x.XboxUserID == s.Name)
                .Select(x => x.DamageTaken)
                .ToArray();

            var avgDamageTaken = damageTaken.Sum() / damageTaken.Count();

            var personalScore = _context.MatchParticipants
                .AsSplitQuery()
                .Where(x => x.XboxUserID == s.Name)
                .Select(x => x.PersonalScore)
                .ToArray();

            var avgPersonalScore = personalScore.Sum() / personalScore.Count();

            worksheet.Cells[row, 1].Value = gamertag;
            worksheet.Cells[row, 2].Value = wins; // Wins
            worksheet.Cells[row, 3].Value = losses; // Losses
            worksheet.Cells[row, 4].Value = s.Score; // Goals
            worksheet.Cells[row, 5].Value = s.Kills - s.PowerWeaponKills; // Ball punches = Kills - Power Weapon Kills
            worksheet.Cells[row, 6].Value = s.Kills;
            worksheet.Cells[row, 7].Value = s.Deaths;
            worksheet.Cells[row, 8].Value = s.Spread;
            worksheet.Cells[row, 9].Value = s.KDRatio;
            worksheet.Cells[row, 10].Value = s.Assists;
            worksheet.Cells[row, 11].Value = timePlayed;
            worksheet.Cells[row, 12].Value = s.KillingSprees;
            worksheet.Cells[row, 13].Value = s.KillingFrenzies;
            worksheet.Cells[row, 14].Value = s.RunningRiots;
            worksheet.Cells[row, 15].Value = s.Rampages;
            worksheet.Cells[row, 16].Value = s.GrandSlam;
            worksheet.Cells[row, 17].Value = s.DoubleKills;
            worksheet.Cells[row, 18].Value = s.TripleKills;
            worksheet.Cells[row, 19].Value = s.Overlills;
            worksheet.Cells[row, 20].Value = s.Killtaculars;
            worksheet.Cells[row, 21].Value = s.Killtrocities;
            worksheet.Cells[row, 22].Value = s.Killamanjaros;
            worksheet.Cells[row, 23].Value = s.Killtastrophe;
            worksheet.Cells[row, 24].Value = s.Killpocalypses;
            worksheet.Cells[row, 25].Value = s.Killionaire;
            worksheet.Cells[row, 26].Value = s.Exterm;
            worksheet.Cells[row, 27].Value = s.Bulltrues;
            worksheet.Cells[row, 28].Value = s.Ninja;
            worksheet.Cells[row, 29].Value = s.Pancake;
            worksheet.Cells[row, 30].Value = s.Whiplash;
            worksheet.Cells[row, 31].Value = s.Killjoys;
            worksheet.Cells[row, 32].Value = s.Harpoons;
            worksheet.Cells[row, 33].Value = avgDamageDealt;
            worksheet.Cells[row, 34].Value = avgDamageTaken;
            worksheet.Cells[row, 35].Value = avgPersonalScore;

            row++;
        }

        worksheet.Cells[1, 1, 1, 35].AutoFitColumns();

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
