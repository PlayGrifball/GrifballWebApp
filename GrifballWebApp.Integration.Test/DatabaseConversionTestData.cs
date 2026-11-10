using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Conversion;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Integration.Test;

/// <summary>A database the app could have written, for the conversion and script export tests.</summary>
internal static class DatabaseConversionTestData
{
    internal static readonly DateTime Precise = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc).AddTicks(1_234_567);

    internal sealed record Filled(int SeasonID, DateTime WhileFirst, int FinalID, int SemiHomeID, int SemiAwayID, int TeamID, int CaptainID,
        int RescheduleID, int SeasonMatchID, int LockedUserID, int CreatorUserID, Guid MatchID);

    internal static async Task<DateTime> PeriodStartOf(GrifballContext db, Season season)
        => await db.Seasons.Where(s => s.SeasonID == season.SeasonID).Select(s => EF.Property<DateTime>(s, "PeriodStart")).SingleAsync();

    /// <summary>
    /// A database the app could have written: rows in most tables, with history (updates and deletes);
    /// rows referring to later rows of their own table (a bracket's final before its semifinals); both
    /// cycles between tables (a team and its captain, a match and its active reschedule); and the types
    /// that differ between the servers (datetimeoffset, the end of time, durations, ulong, bytes, a time
    /// of day, case and accents in text).
    /// </summary>
    internal static async Task<Filled> Fill(DatabaseEndpoint database)
    {
        await using var db = DatabaseConverter.NewContext(database);
        await db.Database.MigrateAsync();

        var creator = User.Seed(userName: "creator", normalizedUserName: "CREATOR", email: "creator@example.com",
            xboxUser: XboxUser.FromXbox(2535405290000001, "Noé O'Brien"));
        // Npgsql writes only UTC offsets; SQL Server keeps any, which Postgres holds as the same instant.
        creator.LockoutEnd = database.Provider == DatabaseProvider.SqlServer
            ? new DateTimeOffset(2026, 11, 1, 8, 30, 0, TimeSpan.FromHours(-5))
            : new DateTimeOffset(2026, 11, 1, 13, 30, 0, TimeSpan.Zero);
        var locked = User.Seed(userName: "locked", normalizedUserName: "LOCKED",
            xboxUser: XboxUser.FromXbox(2535405290000002, "ALPHA bravo"),
            discordUser: DiscordUser.FromDiscord(123456789012345678, "noah"));
        locked.LockoutEnd = DateTimeOffset.MaxValue;
        db.Users.AddRange(creator, locked);
        foreach (var audited in new[] { creator, locked })
        {
            db.Entry(audited).Property(u => u.CreatedAt).CurrentValue = Precise;
            db.Entry(audited).Property(u => u.ModifiedAt).CurrentValue = Precise;
        }
        await db.SaveChangesAsync();
        db.Entry(creator).Property(u => u.CreatedByID).CurrentValue = locked.Id;
        await db.SaveChangesAsync();

        var season = Season.Seed(seasonName: "First").WithDates(publicAt: Precise, signupsOpen: Precise, signupsClose: Precise.AddDays(7), draftStart: Precise.AddDays(8), seasonStart: Precise.AddDays(14), seasonEnd: Precise.AddDays(60));
        db.Seasons.Add(season);
        await db.SaveChangesAsync();
        var first = await PeriodStartOf(db, season);
        await Task.Delay(30);
        season.Edited(seasonName: "Second");
        await db.SaveChangesAsync();
        var second = await PeriodStartOf(db, season);
        await Task.Delay(30);
        season.Edited(seasonName: "Third");
        await db.SaveChangesAsync();

        var team = Team.Seed(season: season, teamName: "Équipe ü");
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        team.SeedPlayer(creator.Id, draftRound: 1, draftPick: 1);
        team.AddPlayer(locked.Id);
        await db.SaveChangesAsync();
        // Apart, as the seasons' versions: two saves in the same tick of SQL Server's clock give the
        // traded player a version that ends as it starts, which temporal queries leave out.
        await Task.Delay(30);
        team.SetCaptain(creator.Id);
        team.RemovePlayer(locked.Id);
        await db.SaveChangesAsync();

        var finalMatch = SeasonMatch.Seed(season, null, null, bestOf: 5, scheduledTime: Precise);
        var semiHomeMatch = SeasonMatch.Seed(season, team, null, bestOf: 3);
        var semiAwayMatch = SeasonMatch.Seed(season, null, null, bestOf: 3);
        db.SeasonMatches.AddRange(finalMatch, semiHomeMatch, semiAwayMatch);
        await db.SaveChangesAsync();
        var final = MatchBracketInfo.Seed(seasonMatch: finalMatch, roundNumber: 2, matchNumber: 1, homeTeamSeedNumber: 1, awayTeamSeedNumber: 2);
        db.Set<MatchBracketInfo>().Add(final);
        await db.SaveChangesAsync();
        var semiHome = MatchBracketInfo.Seed(seasonMatch: semiHomeMatch, roundNumber: 1, matchNumber: 1, homeTeamSeedNumber: 1, awayTeamSeedNumber: 4);
        var semiAway = MatchBracketInfo.Seed(seasonMatch: semiAwayMatch, roundNumber: 1, matchNumber: 2, homeTeamSeedNumber: 2, awayTeamSeedNumber: 3);
        db.Set<MatchBracketInfo>().AddRange(semiHome, semiAway);
        await db.SaveChangesAsync();
        // The final points back at slots saved after it (no factory makes that order): set past the model
        var finalEntry = db.Entry(final);
        finalEntry.Property(b => b.HomeTeamSeedNumber).CurrentValue = null;
        finalEntry.Property(b => b.HomeTeamPreviousMatchBracketInfoID).CurrentValue = semiHome.MatchBracketInfoID;
        finalEntry.Property(b => b.AwayTeamSeedNumber).CurrentValue = null;
        finalEntry.Property(b => b.AwayTeamPreviousMatchBracketInfoID).CurrentValue = semiAway.MatchBracketInfoID;
        await db.SaveChangesAsync();

        var reschedule = MatchReschedule.Request(finalMatch.SeasonMatchID, Precise, null, "Busy", creator.Id, Precise);
        reschedule.AttachThread(ulong.MaxValue);
        db.MatchReschedules.Add(reschedule);
        db.Entry(reschedule).Property(r => r.CommissionerNotes).CurrentValue = "";
        await db.SaveChangesAsync();

        var match = Match.Seed(Guid.NewGuid(), startTime: Precise, duration: TimeSpan.FromTicks(7_123_456_789));
        match.AddTeam(0, 50, Outcomes.Won);
        db.Matches.Add(match);
        await db.SaveChangesAsync();
        match.AddParticipant(0, 2535405290000001, new MatchParticipantStats
        {
            Kda = 1.25f, Accuracy = 33.33333f, AverageLife = TimeSpan.FromTicks(301_234_567), TimePlayed = TimeSpan.FromMinutes(12), FirstJoinedTime = Precise,
        });
        db.MatchedMatches.Add(MatchedMatch.Start(
            new MatchedTeam([]),
            new MatchedTeam([]),
            Precise).Announced(ulong.MaxValue, 1).Reported(match.MatchID));
        db.Ranks.Add(Rank.Create("Onyx", 1500, "#101010", "Top", [0, 1, 2, 254, 255]));
        db.Availability.Add(AvailabilityOption.At(DayOfWeek.Friday, new TimeOnly(19, 30, 15)));
        db.DataProtectionKeys.Add(new DataProtectionKey { FriendlyName = "key-1", Xml = "<key id=\"1\"/>" });
        db.QueuedPlayer.Add(QueuedPlayer.Seed(locked.Id, Precise));
        await db.SaveChangesAsync();
        db.QueuedPlayer.Remove(await db.QueuedPlayer.SingleAsync());
        // A row the migrations seeded, changed: the source's version is the one to keep.
        var region = await db.Regions.OrderBy(r => r.RegionID).FirstAsync();
        db.Entry(region).Property(r => r.RegionName).CurrentValue += " (edited)";
        await db.SaveChangesAsync();

        return new Filled(season.SeasonID, first + (second - first) / 2, final.MatchBracketInfoID, semiHome.MatchBracketInfoID, semiAway.MatchBracketInfoID,
            team.TeamID, team.CaptainID!.Value, reschedule.MatchRescheduleID, finalMatch.SeasonMatchID, locked.Id, creator.Id, match.MatchID);
    }
}
