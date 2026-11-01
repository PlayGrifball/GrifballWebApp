using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>
/// SQLite's query postprocessing (SqliteQueryTranslationPostprocessorFactory): the APPLY that is a join made one,
/// and text compared case-insensitively where SQLite would compare bytes. On a file of each test's own, made from
/// the model; SQLite needs no server, so these run on every leg of the suite.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SqliteQueryTranslationTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), $"grif-sqlite-queries-{Environment.ProcessId}");

    private GrifballContext _context = null!;

    [OneTimeSetUp]
    public static void MakeFolder() => Directory.CreateDirectory(Folder);

    [OneTimeTearDown]
    public static void DeleteFolder()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(Folder, recursive: true);
    }

    [SetUp]
    public async Task SetUp()
    {
        _context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={Path.Combine(Folder, $"{Guid.NewGuid():N}.db")}").Options);
        await _context.Database.EnsureCreatedAsync();
    }

    [TearDown]
    public async Task TearDown() => await _context.DisposeAsync();

    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private async Task<(Team Champions, Team Others)> TwoTeams()
    {
        var season = new Season
        {
            SeasonName = "Winter", PublicAt = Now, SignupsOpen = Now, SignupsClose = Now.AddDays(7), DraftStart = Now.AddDays(8),
            SeasonStart = Now.AddDays(14), SeasonEnd = Now.AddDays(60),
        };
        var champions = new Team { TeamName = "Champions", Season = season };
        var others = new Team { TeamName = "Others", Season = season };
        var user = new User { UserName = "winner" };
        var other = new User { UserName = "teammate" };
        _context.AddRange(champions, others, user, other);
        // Two players' titles of the same tier: one tier, once
        _context.PlayerChampionships.AddRange(
            new PlayerChampionship { Season = season, Team = champions, User = user, Tier = "Gold", GamertagUsed = "winner" },
            new PlayerChampionship { Season = season, Team = champions, User = other, Tier = "Gold", GamertagUsed = "teammate" });
        await _context.SaveChangesAsync();
        return (champions, others);
    }

    // A navigation's Select(...).Distinct() in a projection: OUTER APPLY of a subquery on its foreign key, a LEFT JOIN
    [Test]
    public async Task ACollectionsDistinctValues_AreJoined_EachTeamItsOwn()
    {
        var (champions, others) = await TwoTeams();
        var query = _context.Teams.AsNoTracking().OrderBy(t => t.TeamID)
            .Select(t => new { t.TeamID, Tiers = t.PlayerChampionships.Select(c => c.Tier).Distinct().ToList() });

        var teams = await query.ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(query.ToQueryString(), Does.Contain("LEFT JOIN").And.Not.Contain("APPLY"));
            Assert.That(teams.Select(t => t.TeamID), Is.EqualTo(new[] { champions.TeamID, others.TeamID }));
            Assert.That(teams[0].Tiers, Is.EqualTo(new[] { "Gold" }));
            Assert.That(teams[1].Tiers, Is.Empty);
        });
    }

    // The same in a split query: CROSS APPLY, an INNER JOIN
    [Test]
    public async Task ACollectionsDistinctValues_InASplitQuery_AreJoined()
    {
        var (champions, _) = await TwoTeams();

        var team = await _context.Teams.AsNoTracking().AsSplitQuery().Where(t => t.TeamID == champions.TeamID)
            .Select(t => new { t.TeamName, Titles = t.PlayerChampionships.Select(c => c.Tier).Distinct().ToArray() })
            .SingleAsync();

        Assert.That(team.Titles, Is.EqualTo(new[] { "Gold" }));
    }

    // Every row of a table, with something of the outer row in the projection: CROSS APPLY of a table, a CROSS JOIN
    [Test]
    public async Task EveryRowOfATable_PerOuterRow_IsACrossJoin()
    {
        // The model's own roles (seeded): the user has the first
        var roles = await _context.Roles.OrderBy(r => r.Name).Select(r => new { r.Id, r.Name }).ToListAsync();
        var user = new User { UserName = "someone" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roles[0].Id });
        await _context.SaveChangesAsync();

        var users = await _context.Users.AsNoTracking().AsSplitQuery()
            .Select(u => new { u.Id, Roles = _context.Roles.OrderBy(r => r.Name).Select(r => new { r.Name, Has = r.UserRoles.Any(ur => ur.UserId == u.Id) }).ToList() })
            .ToListAsync();

        Assert.That(roles, Has.Count.GreaterThan(1));
        Assert.That(users.Single().Roles.Select(r => (r.Name, r.Has)), Is.EqualTo(roles.Select((r, i) => (r.Name, i == 0))));
    }

    // The same in a single query: OUTER APPLY of a table, a LEFT JOIN on 1 = 1
    [Test]
    public async Task EveryRowOfATable_PerOuterRow_InASingleQuery_IsALeftJoin()
    {
        var roles = await _context.Roles.OrderBy(r => r.Name).Select(r => r.Name).ToListAsync();
        _context.Users.Add(new User { UserName = "someone" });
        await _context.SaveChangesAsync();

        var users = await _context.Users.AsNoTracking().AsSingleQuery()
            .Select(u => new { u.Id, Roles = _context.Roles.OrderBy(r => r.Name).Select(r => new { r.Name, Has = r.UserRoles.Any(ur => ur.UserId == u.Id) }).ToList() })
            .ToListAsync();

        Assert.That(users.Single().Roles.Select(r => (r.Name, r.Has)), Is.EqualTo(roles.Select(r => (r, false))));
    }

    // The subquery projecting its key already (TeamID), the correlation written inner side first: the key is projected
    // again under a name of its own
    [Test]
    public async Task ASubqueryProjectingItsKey_CorrelatedEitherWayRound_IsJoined()
    {
        var (champions, others) = await TwoTeams();

        var teams = await _context.Teams.AsNoTracking().OrderBy(t => t.TeamID)
            .Select(t => new { t.TeamID, Titles = _context.PlayerChampionships.Where(c => c.TeamID == t.TeamID).Select(c => new { c.TeamID, c.Tier }).Distinct().ToList() })
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(teams[0].Titles.Select(c => (c.TeamID, c.Tier)), Is.EqualTo(new[] { (champions.TeamID, "Gold") }));
            Assert.That(teams[1].TeamID, Is.EqualTo(others.TeamID));
            Assert.That(teams[1].Titles, Is.Empty);
        });
    }

    // A subquery that refers to the outer row other than by an equality isn't a join: refused
    [Test]
    public async Task ACorrelationOtherThanAnEquality_IsStillRefused()
    {
        await TwoTeams();

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => _context.Teams
            .Select(t => new { t.TeamID, Later = _context.Teams.Where(o => o.TeamID > t.TeamID).Select(o => o.TeamName).Distinct().ToList() })
            .ToListAsync());

        Assert.That(e!.Message, Does.Contain("APPLY"));
    }

    // An APPLY that isn't a join (here a TOP per outer row by an inequality) is still refused, as SQLite refuses it
    [Test]
    public async Task AnApplyThatIsntAJoin_IsStillRefused()
    {
        await TwoTeams();

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => _context.Teams
            .Select(t => new { t.TeamID, Next = _context.Teams.Where(o => o.TeamID > t.TeamID).OrderBy(o => o.TeamID).Take(1).Select(o => o.TeamName).ToList() })
            .ToListAsync());

        Assert.That(e!.Message, Does.Contain("APPLY"));
    }

    // A function's result has no collation on SQLite: compared case-insensitively all the same
    [Test]
    public async Task TextFromAFunction_ComparesIgnoringCase()
    {
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 1, Gamertag = "Shadow S16" });
        await _context.SaveChangesAsync();
        var name = "SHADOWS16";

        Assert.Multiple(async () =>
        {
            Assert.That(await _context.XboxUsers.CountAsync(x => x.Gamertag.Replace(" ", "") == name), Is.EqualTo(1));
            Assert.That(await _context.XboxUsers.CountAsync(x => x.Gamertag.Replace(" ", "") != name), Is.Zero);
            Assert.That(await _context.XboxUsers.CountAsync(x => x.Gamertag.Replace(" ", "") == "other"), Is.Zero);
        });
    }
}
