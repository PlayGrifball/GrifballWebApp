using System.Net;
using System.Reflection;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Signups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetCord;
using NetCord.JsonModels;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using User = GrifballWebApp.Database.Models.User;
using DiscordUser = GrifballWebApp.Database.Models.DiscordUser;
using SignupButtonInteractions = GrifballWebApp.Server.Signups.ButtonInteractions;

namespace GrifballWebApp.Test;

internal static class SignupTestData
{
    internal static async Task<Season> OpenSeason(GrifballContext ctx, bool open = true, string name = "Signup Season")
    {
        var now = DateTime.UtcNow;
        var season = new Season
        {
            SeasonName = name,
            SeasonStart = now.AddDays(10),
            SeasonEnd = now.AddDays(40),
            SignupsOpen = open ? now.AddDays(-5) : now.AddDays(-10),
            SignupsClose = open ? now.AddDays(5) : now.AddDays(-1),
        };
        ctx.Seasons.Add(season);
        await ctx.SaveChangesAsync();
        return season;
    }

    /// <summary>Adds availability options (with odd minutes so they never clash with seeded options) and attaches them to the season.</summary>
    internal static async Task<AvailabilityOption[]> Options(GrifballContext ctx, Season season, params (DayOfWeek day, TimeOnly time)[] slots)
    {
        var options = slots.Select(s => new AvailabilityOption { DayOfWeek = s.day, Time = s.time }).ToArray();
        ctx.Availability.AddRange(options);
        await ctx.SaveChangesAsync();
        ctx.SeasonAvailability.AddRange(options.Select(o => new SeasonAvailability { SeasonID = season.SeasonID, AvailabilityOptionID = o.AvailabilityOptionID }));
        await ctx.SaveChangesAsync();
        return options;
    }
}
