using GrifballWebApp.Application;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// A real <see cref="GrifballContext"/> on a temp-file SQLite database, for the Server-layer classes that query it
/// directly (<see cref="GrifballWebApp.Server.Commands.CommandLookups"/>, <see cref="GrifballWebApp.Server.Commands.DiscordCaller"/>,
/// and the command handlers that use them): no database server, no host (#402). A fresh file per test case
/// (<see cref="LifeCycle.InstancePerTestCase"/>), so tests run in parallel; the model's seed data (the three roles) comes
/// with it.
/// </summary>
public abstract class SqliteGrifballContextFixture
{
    protected GrifballContext Db { get; private set; } = null!;

    private string _path = null!;

    [SetUp]
    public async Task SetUpDb()
    {
        _path = Path.Combine(Path.GetTempPath(), $"grif-server-test-{Guid.NewGuid():N}.db");
        // Pooling=False: with ~350 tests in parallel, a pooled connection's close would otherwise go back to a shared
        // pool another test's teardown might clear (SqliteConnection.ClearAllPools empties every pool in the process),
        // forcing it to reopen. Each test's own file is never reused, so nothing is gained by pooling it anyway.
        Db = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={_path};Pooling=False").Options);
        try
        {
            await Db.Database.EnsureCreatedAsync();
        }
        catch
        {
            // NUnit skips [TearDown] when [SetUp] throws: clean up here so a failure doesn't leave the context open
            // and the temp file behind.
            await Db.DisposeAsync();
            if (File.Exists(_path))
                File.Delete(_path);
            throw;
        }
    }

    [TearDown]
    public async Task TearDownDb()
    {
        await Db.DisposeAsync();
        if (File.Exists(_path))
            File.Delete(_path);
    }

    /// <summary>A user with no gamertag or roles: not staff, and <see cref="GrifballWebApp.Server.Commands.DiscordCaller.Find"/>
    /// won't recognize them as a player either (they need a gamertag).</summary>
    protected async Task<User> AddDiscordOnlyUser(ulong discordUserId, string name = "someone")
    {
        var user = User.Register(name);
        // The object overload, not the raw ID one: User.DiscordUserID is a foreign key to the Discord row, so both must
        // be inserted together.
        user.LinkDiscord(DiscordUser.FromDiscord((long)discordUserId, name));
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        return user;
    }

    /// <summary>A user tied to a Discord account and a gamertag, with <paramref name="roles"/> (by name, matching the
    /// seeded roles: Sysadmin, Commissioner, Player).</summary>
    protected async Task<User> AddUser(ulong discordUserId, string gamertag, params string[] roles)
    {
        var user = User.Register(gamertag);
        user.LinkDiscord(DiscordUser.FromDiscord((long)discordUserId, gamertag));
        user.TieToXbox(XboxUser.FromXbox((long)discordUserId, gamertag));
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        foreach (var roleName in roles)
        {
            var role = await Db.Roles.SingleAsync(x => x.Name == roleName);
            Db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        }
        if (roles.Length > 0)
            await Db.SaveChangesAsync();
        return user;
    }

    protected async Task<Season> AddSeason(string name)
    {
        var season = Season.Seed(seasonName: name);
        Db.Seasons.Add(season);
        await Db.SaveChangesAsync();
        return season;
    }

    protected async Task<Team> AddTeam(Season season, string name)
    {
        var team = Team.Seed(teamName: name, season: season);
        Db.Teams.Add(team);
        await Db.SaveChangesAsync();
        return team;
    }

    protected async Task<SeasonMatch> AddSeasonMatch(Season season, Team home, Team away, int bestOf = 3)
    {
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: bestOf);
        Db.SeasonMatches.Add(match);
        await Db.SaveChangesAsync();
        return match;
    }

    /// <summary>A UrlService whose links resolve against a fixed base URL, for assertions that check a link's shape.</summary>
    protected static UrlService Urls() =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());
}
