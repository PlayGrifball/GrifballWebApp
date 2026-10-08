using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Reschedule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Reflection;
using System.Security.Claims;

namespace GrifballWebApp.Test;

internal static class RescheduleTestData
{
    public record Seeded(Season Season, Team Home, Team Away, User HomeCaptain, User AwayCaptain, User Requester, SeasonMatch Match);

    public static async Task<User> AddUser(GrifballContext context, string name, long? xboxId = null, long? discordId = null, string? displayName = null)
    {
        var user = new User { UserName = name, DisplayName = displayName };
        if (xboxId is not null)
            user.XboxUser = new XboxUser { XboxUserID = xboxId.Value, Gamertag = $"GT_{name}" };
        if (discordId is not null)
            user.DiscordUser = new GrifballWebApp.Database.Models.DiscordUser { DiscordUserID = discordId.Value, DiscordUsername = $"dc_{name}" };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    public static async Task<Team> AddTeamWithCaptain(GrifballContext context, Season season, string name, User captain)
    {
        var team = new Team { SeasonID = season.SeasonID, TeamName = name };
        context.Teams.Add(team);
        await context.SaveChangesAsync();
        var tp = new TeamPlayer { TeamID = team.TeamID, UserID = captain.Id };
        context.TeamPlayers.Add(tp);
        await context.SaveChangesAsync();
        team.CaptainID = tp.TeamPlayerID;
        await context.SaveChangesAsync();
        return team;
    }

    public static async Task<Seeded> SeedAsync(GrifballContext context, DateTime? scheduled = null, bool captainsHaveDiscord = true)
    {
        var season = new Season { SeasonName = "Reschedule Season" };
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        var homeCap = await AddUser(context, "homecap", 1001, captainsHaveDiscord ? 5001 : null);
        var awayCap = await AddUser(context, "awaycap", 1002, captainsHaveDiscord ? 5002 : null);
        var requester = await AddUser(context, "requester", 1003, 5003);
        var home = await AddTeamWithCaptain(context, season, "Home", homeCap);
        var away = await AddTeamWithCaptain(context, season, "Away", awayCap);
        var match = new SeasonMatch { SeasonID = season.SeasonID, HomeTeamID = home.TeamID, AwayTeamID = away.TeamID, BestOf = 3, ScheduledTime = scheduled };
        context.SeasonMatches.Add(match);
        await context.SaveChangesAsync();
        return new Seeded(season, home, away, homeCap, awayCap, requester, match);
    }

    public static async Task<MatchReschedule> AddReschedule(GrifballContext context, int seasonMatchID, int requesterId,
        RescheduleStatus status = RescheduleStatus.Pending, DateTime? newTime = null, DateTime? requestedAt = null, ulong? threadId = null, DateTime? original = null)
    {
        var r = new MatchReschedule
        {
            SeasonMatchID = seasonMatchID,
            RequestedByUserID = requesterId,
            Reason = "Cannot make it",
            Status = status,
            NewScheduledTime = newTime,
            OriginalScheduledTime = original,
            RequestedAt = requestedAt ?? DateTime.UtcNow,
            DiscordThreadID = threadId,
        };
        context.MatchReschedules.Add(r);
        await context.SaveChangesAsync();
        return r;
    }

}
