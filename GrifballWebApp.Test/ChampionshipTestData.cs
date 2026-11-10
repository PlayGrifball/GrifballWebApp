using GrifballWebApp.Infrastructure.Queries;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>A test's titles, of any tier of a season of either kind, recorded through the season (<see cref="Season.SeedChampions"/>).</summary>
internal static class ChampionshipTestData
{
    /// <summary>
    /// Makes <paramref name="teamID"/> the champions of the season's <paramref name="tier"/> as the season records them:
    /// a title for each of <paramref name="members"/> (the team's roster when null, its captain flagged), the tier's
    /// other titles gone; a null team takes the tier's titles away. Saves.
    /// </summary>
    public static async Task<ChampionshipChanges> SeedChampionsAsync(this GrifballContext context, int seasonID, string tier, int? teamID,
        IReadOnlyCollection<ChampionMember>? members = null, int? gameVersionID = null)
    {
        var season = await context.Seasons.WithChampionships().SingleAsync(s => s.SeasonID == seasonID);
        var team = teamID is int id ? await context.Teams.SingleAsync(t => t.TeamID == id) : null;
        members ??= team is null ? [] : await RosterAsync(context, team);
        var changes = season.SeedChampions(tier, team!, members, gameVersionID);
        await context.SaveChangesAsync();
        return changes;
    }

    /// <summary>The team's roster as a title records it (ChampionshipService's): its players in order, named as the app names them.</summary>
    private static async Task<List<ChampionMember>> RosterAsync(GrifballContext context, Team team)
    {
        var players = await context.Set<TeamPlayer>().AsNoTracking().Where(tp => tp.TeamID == team.TeamID).OrderBy(tp => tp.TeamPlayerID)
            .Select(tp => new
            {
                tp.TeamPlayerID,
                tp.UserID,
                Gamertag = tp.User.XboxUser != null ? tp.User.XboxUser.Gamertag : null,
                Discord = tp.User.DiscordUser != null ? tp.User.DiscordUser.DiscordUsername : null,
                tp.User.DisplayName,
                tp.User.UserName,
            }).ToListAsync();
        return players.Select(p => new ChampionMember(p.UserID, UserNames.Pick(p.Gamertag, p.Discord, p.DisplayName, p.UserName), p.TeamPlayerID == team.CaptainID)).ToList();
    }
}
