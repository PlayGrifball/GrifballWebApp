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
        var season = Season.Seed(seasonName: "Winter").WithDates(publicAt: Now, signupsOpen: Now, signupsClose: Now.AddDays(7), draftStart: Now.AddDays(8), seasonStart: Now.AddDays(14), seasonEnd: Now.AddDays(60));
        var champions = new Team { TeamName = "Champions", Season = season };
        var others = new Team { TeamName = "Others", Season = season };
        var user = new User { UserName = "winner" };
        var other = new User { UserName = "teammate" };
        _context.AddRange(champions, others, user, other);
        // Two players' titles of the same tier: one tier, once
        _context.PlayerChampionships.AddRange(
            PlayerChampionship.Seed(season: season, team: champions, user: user, tier: "Gold", gamertagUsed: "winner"),
            PlayerChampionship.Seed(season: season, team: champions, user: other, tier: "Gold", gamertagUsed: "teammate"));
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

    // The correlation written inner side first, beside a filter of the subquery's own, which stays in it
    [Test]
    public async Task ASubqueryCorrelatedInnerSideFirst_WithAFilterOfItsOwn_IsJoined()
    {
        var (champions, others) = await TwoTeams();
        var query = _context.Teams.AsNoTracking().OrderBy(t => t.TeamID)
            .Select(t => new { t.TeamID, Titles = _context.PlayerChampionships.Where(c => c.TeamID == t.TeamID && c.Tier != "Silver").Select(c => c.Tier).Distinct().ToList() });

        var teams = await query.ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(query.ToQueryString(), Does.Contain("JOIN").And.Contain("Silver").And.Not.Contain("APPLY"));
            Assert.That(teams[0].Titles, Is.EqualTo(new[] { "Gold" }));
            Assert.That(teams[1].TeamID, Is.EqualTo(others.TeamID));
            Assert.That(teams[1].Titles, Is.Empty);
            Assert.That(teams[0].TeamID, Is.EqualTo(champions.TeamID));
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

    // An equality whose outer side is an expression, not a column of the outer row's: not a join on keys, refused
    [Test]
    public async Task AnEqualityOnAnExpressionOfTheOuterRow_IsStillRefused()
    {
        await TwoTeams();

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => _context.Teams
            .Select(t => new { t.TeamID, Next = _context.Teams.Where(o => o.TeamID == t.TeamID + 1).Select(o => o.TeamName).Distinct().ToList() })
            .ToListAsync());

        Assert.That(e!.Message, Does.Contain("APPLY"));
    }

    // Temporal queries' SQL on SQLite (TemporalSql.Sqlite): tables by name alone, times as EF stores them, unrounded
    [Test]
    public void TemporalQueries_NameTablesAlone_AndWriteTimesAsEFStoresThem()
    {
        var season = _context.Model.FindEntityType(typeof(Season))!;
        var asOf = new Microsoft.EntityFrameworkCore.SqlServer.Query.Internal.TemporalAsOfQueryRootExpression(season, new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc).AddTicks(1234567));

        Assert.Multiple(() =>
        {
            Assert.That(TemporalSql.Sqlite.Name("Event", "Seasons"), Is.EqualTo("\"Seasons\""));
            Assert.That(TemporalSql.Sqlite.Literal(DateTime.MaxValue), Is.EqualTo("'9999-12-31 23:59:59.9999999'"), "the current rows' PeriodEnd");
            Assert.That(TemporalSql.Sqlite.Literal(new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc)), Is.EqualTo("'2026-10-10 12:00:00'"));
            Assert.That(TemporalSql.Sqlite.Instants(asOf), Is.EqualTo(new[] { asOf.PointInTime }), "100ns, not rounded");
            Assert.Throws<InvalidOperationException>(() => TemporalSql.Sqlite.Instant(DateTime.Now));
            Assert.That(PostgresTemporalQueries.Sql(asOf, TemporalSql.Sqlite),
                Does.Contain("FROM \"Seasons\" WHERE").And.Contain("UNION ALL").And.Contain("FROM \"SeasonsHistory\"")
                .And.Contain("'2026-10-10 12:00:00.1234567'").And.Not.Contain("\"Event\""));
        });
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
