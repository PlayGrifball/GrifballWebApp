using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Controllers;
using GrifballWebApp.Server.Grades;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Collections;
using System.Reflection;
using Match = GrifballWebApp.Database.Models.Match;

namespace GrifballWebApp.Test;

internal static class GradesTestData
{
    public static async Task SeedMedals(GrifballContext context)
    {
        context.MedalTypes.AddRange(
            new MedalType { MedalTypeID = 1, MedalTypeName = "Spree" },
            new MedalType { MedalTypeID = 2, MedalTypeName = "Mode" },
            new MedalType { MedalTypeID = 3, MedalTypeName = "Multikill" });
        context.MedalDifficulties.AddRange(
            new MedalDifficulty { MedalDifficultyID = 1, MedalDifficultyName = "Normal" },
            new MedalDifficulty { MedalDifficultyID = 2, MedalDifficultyName = "Heroic" },
            new MedalDifficulty { MedalDifficultyID = 3, MedalDifficultyName = "Legendary" },
            new MedalDifficulty { MedalDifficultyID = 4, MedalDifficultyName = "Mythic" });
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

        static Medal M(long id, string name, int type, int difficulty) => new()
        {
            MedalID = id, MedalName = name, Description = name, MedalTypeID = type, MedalDifficultyID = difficulty,
        };
    }

    public static async Task<(Season season, SeasonMatch seasonMatch)> SeedSeason(GrifballContext context, string name = "Grades Season")
    {
        var season = new Season { SeasonName = name };
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        var sm = new SeasonMatch { SeasonID = season.SeasonID, BestOf = 5 };
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
                context.XboxUsers.Add(new XboxUser { XboxUserID = p.Xbox, Gamertag = $"GT{p.Xbox}" });
        }
        var id = Guid.NewGuid();
        var match = new Match
        {
            MatchID = id,
            Duration = duration,
            StartTime = DateTime.UtcNow,
            MatchTeams = new List<MatchTeam>
            {
                new()
                {
                    MatchID = id, TeamID = 0, Outcome = Outcomes.Won,
                    MatchParticipants = players.Select(p => new MatchParticipant
                    {
                        MatchID = id, TeamID = 0, XboxUserID = p.Xbox, Score = p.Score, Kills = p.Kills, Deaths = p.Deaths, PowerWeaponKills = p.PowerWeaponKills,
                        MedalEarned = (p.Medals ?? new()).Select(m => new MedalEarned { MedalID = m.Key, MatchID = id, XboxUserID = p.Xbox, Count = m.Value }).ToList(),
                    }).ToList(),
                },
            },
        };
        context.Matches.Add(match);
        if (seasonMatchID is not null)
            context.MatchLinks.Add(new MatchLink { MatchID = id, SeasonMatchID = seasonMatchID.Value, MatchNumber = matchNumber });
        await context.SaveChangesAsync();
        return match;
    }
}
