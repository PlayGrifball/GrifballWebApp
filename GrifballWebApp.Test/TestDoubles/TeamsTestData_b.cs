using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test.CovB;

/// <summary>Seeding helpers for the draft/teams tests.</summary>
internal static class TeamsTestData_b
{
    public const int CommissionerRoleId = 2;

    public static async Task<Season> AddSeason(this GrifballContext context, string name = "Season", bool captainsLocked = false)
    {
        var season = new Season
        {
            SeasonName = name,
            SeasonStart = new DateTime(2025, 1, 1),
            SeasonEnd = new DateTime(2025, 3, 1),
            CaptainsLocked = captainsLocked,
        };
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        return season;
    }

    private static long _nextExternalId = 100_000;

    public static async Task<User> AddUser(this GrifballContext context, string userName, string? displayName = null,
        string? gamertag = null, string? discordName = null, bool commissioner = false)
    {
        var user = new User { UserName = userName, DisplayName = displayName };
        if (gamertag is not null)
            user.XboxUser = new XboxUser { XboxUserID = Interlocked.Increment(ref _nextExternalId), Gamertag = gamertag };
        if (discordName is not null)
            user.DiscordUser = new DiscordUser { DiscordUserID = Interlocked.Increment(ref _nextExternalId), DiscordUsername = discordName };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        if (commissioner)
        {
            context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = CommissionerRoleId });
            await context.SaveChangesAsync();
        }
        return user;
    }

    public static async Task<SeasonSignup> AddSignup(this GrifballContext context, Season season, User user, string? teamName = null)
    {
        var signup = new SeasonSignup { SeasonID = season.SeasonID, UserID = user.Id, Timestamp = new DateTime(2024, 12, 1), TeamName = teamName };
        context.SeasonSignups.Add(signup);
        await context.SaveChangesAsync();
        return signup;
    }

    /// <summary>Creates a team captained by <paramref name="captain"/> plus non-captain players in the given draft-round order.</summary>
    public static async Task<Team> AddTeam(this GrifballContext context, Season season, string teamName, User captain, int captainOrder, params User[] players)
    {
        var team = new Team { SeasonID = season.SeasonID, TeamName = teamName };
        var captainTp = new TeamPlayer { UserID = captain.Id, DraftCaptainOrder = captainOrder };
        team.TeamPlayers.Add(captainTp);
        var round = 1;
        foreach (var p in players)
            team.TeamPlayers.Add(new TeamPlayer { UserID = p.Id, DraftRound = round++ });
        context.Teams.Add(team);
        await context.SaveChangesAsync();
        team.Captain = captainTp;
        await context.SaveChangesAsync();
        return team;
    }

    public static GrifballContext NewContextLike(this GrifballContext context)
    {
        return new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseSqlServer(context.Database.GetConnectionString()).Options);
    }

    public static IDbContextFactory<GrifballContext> FactoryFor(GrifballContext context)
    {
        var factory = NSubstitute.Substitute.For<IDbContextFactory<GrifballContext>>();
        NSubstitute.SubstituteExtensions.Returns(factory.CreateDbContext(), _ => context.NewContextLike());
        NSubstitute.SubstituteExtensions.Returns(factory.CreateDbContextAsync(NSubstitute.Arg.Any<CancellationToken>()), _ => Task.FromResult(context.NewContextLike()));
        return factory;
    }
}
