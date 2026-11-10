using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Match = GrifballWebApp.Domain.Halo.Match;

namespace GrifballWebApp.Integration.Test;

internal static class GradesTestData
{
    public static async Task SeedMedals(GrifballContext context)
    {
        context.MedalTypes.AddRange(
            MedalType.Imported(1, "Spree"),
            MedalType.Imported(2, "Mode"),
            MedalType.Imported(3, "Multikill"));
        context.MedalDifficulties.AddRange(
            MedalDifficulty.Imported(1, "Normal"),
            MedalDifficulty.Imported(2, "Heroic"),
            MedalDifficulty.Imported(3, "Legendary"),
            MedalDifficulty.Imported(4, "Mythic"));
        context.Medals.AddRange(
            M(1, "Killing Spree", 1, 1),
            M(2, "Killjoy", 1, 1),
            M(3, "Double Kill", 3, 1),
            M(4, "Triple Kill", 3, 2),
            M(5, "Overkill", 3, 4),
            M(6, "Grand Slam", 2, 1),
            M(7, "Pancake", 2, 1),
            M(8, "Whiplash", 2, 1),
            M(9, "Killtacular", 3, 3)); // multikill but difficulty 3 => not counted
        await context.SaveChangesAsync();

        static Medal M(long id, string name, int type, int difficulty)
            => Medal.Imported(id, name, name, spriteIndex: 0, sortingWeight: 0, medalDifficultyID: difficulty, medalTypeID: type, personalScore: 0);
    }

    public static async Task<(Season season, SeasonMatch seasonMatch)> SeedSeason(GrifballContext context, string name = "Grades Season")
    {
        var season = Season.Seed(seasonName: name);
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        var sm = SeasonMatch.Seed(season.SeasonID, null, null, bestOf: 5);
        context.SeasonMatches.Add(sm);
        await context.SaveChangesAsync();
        return (season, sm);
    }

    public record P(long Xbox, int Score, int Kills, int Deaths, int PowerWeaponKills, Dictionary<long, int>? Medals = null);

    public static async Task<Match> AddLinkedMatch(GrifballContext context, int? seasonMatchID, int matchNumber, TimeSpan duration, params P[] players)
    {
        foreach (var p in players)
        {
            if (await context.XboxUsers.FindAsync(p.Xbox) is null)
                context.XboxUsers.Add(XboxUser.FromXbox(p.Xbox, $"GT{p.Xbox}"));
        }
        var id = Guid.NewGuid();
        var match = Match.Seed(id, startTime: DateTime.UtcNow, duration: duration);
        match.AddTeam(0, 0, Outcomes.Won);
        foreach (var p in players)
            match.AddParticipant(0, p.Xbox, new MatchParticipantStats { Score = p.Score, Kills = p.Kills, Deaths = p.Deaths, PowerWeaponKills = p.PowerWeaponKills },
                (p.Medals ?? new()).Select(m => new MedalCount(m.Key, m.Value)));
        context.Matches.Add(match);
        if (seasonMatchID is not null)
            context.Set<SeriesGame>().Add(new SeriesGame { MatchID = id, SeasonMatchID = seasonMatchID.Value, GameNumber = matchNumber });
        await context.SaveChangesAsync();
        return match;
    }
}
