using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace GrifballWebApp.Server.Grades;

public class GradesService
{
    private readonly GrifballContext _context;
    public GradesService(GrifballContext grifballContext)
    {
        _context = grifballContext;
    }

    public async Task<GradesDto> GetGrades(int seasonID, CancellationToken ct)
    {
        var stats = await _context.MatchParticipants
            .Include(x => x.MedalEarned)
                .ThenInclude(x => x.Medal)
            .Where(x => x.MatchTeam.Match.MatchLink.SeasonMatch.SeasonID == seasonID)
            .GroupBy(x => x.XboxUserID)
            .Select(x => new TotalDto()
            {
                XboxUserID = x.Key,
                Gamertag = x.First().XboxUser.Gamertag,
                TotalGoals = x.Sum(x => x.Score),// Goals
                //TotalKDRatio = (decimal)x.Sum(x => x.Kills) / (decimal)x.Sum(x => x.Deaths),
                TotalKDSpread = x.Sum(x => x.Kills) - x.Sum(x => x.Deaths),
                //TotalDeaths = x.Sum(x => x.Deaths),
                TotalPunches = x.Sum(x => x.Kills) - x.Sum(x => x.PowerWeaponKills), // Ball punches = Kills - Power Weapon Kills
                TotalSprees = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalTypeID == 1 && z.Medal.MedalName != "Killjoy").Select(s => s.Count)).Sum(),
                TotalDoubleKills = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Double Kill").Select(s => s.Count)).Sum(),
                TotalTripleKills = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Triple Kill").Select(s => s.Count)).Sum(),
                // Overkill and above difficulty is greater than 3
                TotalMultiKills = x.SelectMany(b => b.MedalEarned
                                                // Overkill and higher OR Grand Slam
                                                .Where(z => (z.Medal.MedalTypeID == 3 && z.Medal.MedalDifficultyID > 3) || z.Medal.MedalName == "Grand Slam")
                                                .Select(s => s.Count)).Sum(),
                TotalXFactor = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Pancake" || z.Medal.MedalName == "Killjoy" || z.Medal.MedalName == "Whiplash").Select(s => s.Count)).Sum(),
                TotalKills = x.Sum(x => x.Kills),
                // Game time - I see no simple way to get this
                TotalGameTime = 0.0, // placeholder
                
                //KillingSprees = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Killing Spree").Select(s => s.Count)).Sum(),
                //KillingFrenzies = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Killing Frenzy").Select(s => s.Count)).Sum(),
                //RunningRiots = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Running Riot").Select(s => s.Count)).Sum(),
                //Rampages = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Rampage").Select(s => s.Count)).Sum(),
                //GrandSlam = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Grand Slam").Select(s => s.Count)).Sum(),
                //DoubleKills = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Double Kill").Select(s => s.Count)).Sum(),
                //TripleKills = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Triple Kill").Select(s => s.Count)).Sum(),
                //OverKills = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Overkill").Select(s => s.Count)).Sum(),
                //Killtaculars = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Killtacular").Select(s => s.Count)).Sum(),
                //Killtrocities = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Killtrocity").Select(s => s.Count)).Sum(),
                //Killamanjaros = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Killamanjaro").Select(s => s.Count)).Sum(),
                //Killtastrophe = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Killtastrophe").Select(s => s.Count)).Sum(),
                //Killpocalypses = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Killpocalypse").Select(s => s.Count)).Sum(),
                //Killionaire = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Killionaire").Select(s => s.Count)).Sum(),
                //Exterm = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Extermination").Select(s => s.Count)).Sum(),
                //Bulltrues = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Bulltrue").Select(s => s.Count)).Sum(),
                //Ninja = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Ninja").Select(s => s.Count)).Sum(),
                //Pancake = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Pancake").Select(s => s.Count)).Sum(),
                //Whiplash = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Whiplash").Select(s => s.Count)).Sum(),
                //Killjoys = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Killjoy").Select(s => s.Count)).Sum(),
                //Harpoons = x.SelectMany(b => b.MedalEarned.Where(z => z.Medal.MedalName == "Harpoon").Select(s => s.Count)).Sum(),
            })
            .OrderBy(x => x.Gamertag)
            .ToListAsync(ct);

        var allTimesPlayed = await _context.MatchParticipants
            .Where(x => x.MatchTeam.Match.MatchLink.SeasonMatch.SeasonID == seasonID)
            .GroupBy(x => x.XboxUserID)
            .Select(x => new
            {
                XboxID = x.Key,
                Duration = x.Select(x => x.MatchTeam.Match.Duration)
            })
            .ToListAsync(ct);

        var sumAllTimesPlayed = allTimesPlayed.Select(x => new
        {
            XboxID = x.XboxID,
            TotalTimePlayed = x.Duration.Select(b => b.TotalMinutes).Sum(),
        });

        var pm = new List<PerMinuteDto>();
        foreach (var x in stats)
        {
            var totalForPlayer = sumAllTimesPlayed.FirstOrDefault(y => y.XboxID == x.XboxUserID)?.TotalTimePlayed;
            if (totalForPlayer == null)
                continue;

            x.TotalGameTime = totalForPlayer.Value; // Back assign total game time now since it could not be done with sql

            pm.Add(new PerMinuteDto()
            {
                XboxUserID = x.XboxUserID,
                Gamertag = x.Gamertag,
                GoalsPM = x.TotalGoals / x.TotalGameTime,
                KDSpreadPM = x.TotalKDSpread / x.TotalGameTime,
                PunchesPM = x.TotalPunches / x.TotalGameTime,
                SpreesPM = x.TotalSprees / x.TotalGameTime,
                DoubleKillsPM = x.TotalDoubleKills / x.TotalGameTime,
                TripleKillsPM = x.TotalTripleKills / x.TotalGameTime,
                MultiKillsPM = x.TotalMultiKills / x.TotalGameTime,
                XFactorPM = x.TotalXFactor / x.TotalGameTime,
                KillsPM = x.TotalKills / x.TotalGameTime,
            });
        }


        var letters = GetLetters(false);

        var goalPercentiles = GetLetterPercentiles(letters, pm, x => x.GoalsPM);
        var kdSpreadPercentiles = GetLetterPercentiles(letters, pm, x => x.KDSpreadPM);
        var punchesPercentiles = GetLetterPercentiles(letters, pm, x => x.PunchesPM);
        var spreesPercentiles = GetLetterPercentiles(letters, pm, x => x.SpreesPM);
        var doubleKillsPercentiles = GetLetterPercentiles(letters, pm, x => x.DoubleKillsPM);
        var tripleKillsPercentiles = GetLetterPercentiles(letters, pm, x => x.TripleKillsPM);
        var multiKillsPercentiles = GetLetterPercentiles(letters, pm, x => x.MultiKillsPM);
        var xFactorPercentiles = GetLetterPercentiles(letters, pm, x => x.XFactorPM);
        var killsPercentiles = GetLetterPercentiles(letters, pm, x => x.KillsPM);

        var playerGrades = pm.Select(x => new LetterDto()
        {
            XboxUserID = x.XboxUserID,
            Gamertag = x.Gamertag,
            Goals = GetLetter(goalPercentiles, x.GoalsPM),
            KDSpread = GetLetter(kdSpreadPercentiles, x.KDSpreadPM),
            Punches = GetLetter(punchesPercentiles, x.PunchesPM),
            Sprees = GetLetter(spreesPercentiles, x.SpreesPM),
            DoubleKills = GetLetter(doubleKillsPercentiles, x.DoubleKillsPM),
            TripleKills = GetLetter(tripleKillsPercentiles, x.TripleKillsPM),
            MultiKills = GetLetter(multiKillsPercentiles, x.MultiKillsPM),
            XFactor = GetLetter(xFactorPercentiles, x.XFactorPM),
            Kills = GetLetter(killsPercentiles, x.KillsPM),
            GradeAvgMath = (.40d * GetLetterValue(goalPercentiles, x.GoalsPM)) +
                           (.55d * GetLetterValue(kdSpreadPercentiles, x.KDSpreadPM)) +
                           (.15d * GetLetterValue(punchesPercentiles, x.PunchesPM)) +
                           (.35d * GetLetterValue(spreesPercentiles, x.SpreesPM)) +
                           (.20d * GetLetterValue(doubleKillsPercentiles, x.DoubleKillsPM)) +
                           (.25d * GetLetterValue(tripleKillsPercentiles, x.TripleKillsPM)) +
                           (.35d * GetLetterValue(multiKillsPercentiles, x.MultiKillsPM)) +
                           (.10d * GetLetterValue(xFactorPercentiles, x.XFactorPM)) +
                           (.25d * GetLetterValue(killsPercentiles, x.KillsPM))
        }).ToList();

        var letterPercentiles = GetLetterPercentiles(letters, playerGrades, x => x.GradeAvgMath);

        foreach (var pg in playerGrades)
        {
            pg.GradeAvg = GetLetter(letterPercentiles, pg.GradeAvgMath);
        }

        return new GradesDto()
        {
            Totals = stats,
            PerMinutes = pm,
            Letters = playerGrades,
        };
    }

    private string GetLetter(IEnumerable<LetterPercentile> percentiles, double value) => GetLetterPercentile(percentiles, value).Letter.Name;

    private double GetLetterValue(IEnumerable<LetterPercentile> percentiles, double value)
    {
        var p = GetLetterPercentile(percentiles, value);
        return p.Letter.Value;
    }

    private LetterPercentile GetLetterPercentile(IEnumerable<LetterPercentile> percentiles, double value)
    {
        var percentile = percentiles.FirstOrDefault(x => value >= x.Value);

        percentile ??= percentiles.Last(); // F-

        return percentile;
    }

    private IEnumerable<LetterPercentile> GetLetterPercentiles<TSource>(IEnumerable<Letter> letters, IEnumerable<TSource> source, Func<TSource, double> selector)
    {
        
        return letters.Select(x => new LetterPercentile()
        {
            Letter = x,
            Value = Percentile(source.Select(selector.Invoke).ToArray(), x.Seed),
        });
    }

    private List<Letter> GetLetters(bool curved)
    {
        if (curved)
        {
            return new List<Letter>()
            {
                new("S+", 11, 47/48d),
                new("S", 10.5, 46/48d),
                new("S-", 10, 45/48d),
                new("A+", 9.5, 43/48d),
                new("A", 9, 41/48d),
                new("A-", 8.5, 39/48d),
                new("B+", 8, 36/48d),
                new("B", 7.5, 33/48d),
                new("B-", 7, 30/48d),
                new("C+", 6.5, 26/48d),
                new("C", 6, 22/48d),
                new("C-", 5.5, 18/48d),
                new("D+", 5, 15/48d),
                new("D", 4.5, 12/48d),
                new("D-", 4, 9/48d),
                new("E+", 3.5, 7/48d),
                new("E", 3, 5/48d),
                new("E-", 2.5, 3/48d),
                new("F+", 2, 2/48d),
                new("F", 1.5, 1/48d),
                new("F-", 1, 0),
            };
        }
        else
        {
            return new List<Letter>()
            {
                new("S+", 11, 20/21d),
                new("S", 10.5, 19/21d),
                new("S-", 10, 18/21d),
                new("A+", 9.5, 17/21d),
                new("A", 9, 16/21d),
                new("A-", 8.5, 15/21d),
                new("B+", 8, 14/21d),
                new("B", 7.5, 13/21d),
                new("B-", 7, 12/21d),
                new("C+", 6.5, 11/21d),
                new("C", 6, 10/21d),
                new("C-", 5.5, 9/21d),
                new("D+", 5, 8/21d),
                new("D", 4.5, 7/21d),
                new("D-", 4, 6/21d),
                new("E+", 3.5, 5/21d),
                new("E", 3, 4/21d),
                new("E-", 2.5, 3/21d),
                new("F+", 2, 2/21d),
                new("F", 1.5, 1/21d),
                new("F-", 1, 0),
            };
        }
    }

    private double Percentile(double[] sequence, double excelPercentile)
    {
        Array.Sort(sequence);
        int N = sequence.Length;
        double n = (N - 1) * excelPercentile + 1;
        // Another method: double n = (N + 1) * excelPercentile;
        if (n == 1d) return sequence[0];
        else if (n == N) return sequence[N - 1];
        else
        {
            int k = (int)n;
            double d = n - k;
            return sequence[k - 1] + d * (sequence[k] - sequence[k - 1]);
        }
    }
}
