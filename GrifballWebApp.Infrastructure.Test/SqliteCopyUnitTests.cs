using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure.Conversion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// DatabaseConverter's copy (CopyAsync) and comparison (CompareAsync) between two SQLite files made from the model
/// (EnsureCreated): the source has users, a season and a drafted team whose captain closes a foreign key cycle (set
/// after every row is in), and the users' renames left row history. No migrations, so not ConvertAsync, whose source
/// and target checks need them (the Migrations tests'): only what it runs once they pass. SQLite's own reading and
/// writing (SqliteDialect, SqliteTargetWriter, SqliteColumns) and the table plan are what this goes through.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SqliteCopyUnitTests : SqliteContextFixture
{
    private readonly RecordingLogger<DatabaseConverter> _log = new();
    private readonly DatabaseConverter _converter;

    public SqliteCopyUnitTests() => _converter = new DatabaseConverter(_log);
    private string _targetPath = null!;

    private DatabaseEndpoint Source => new(DatabaseProvider.Sqlite, ConnectionString);
    private DatabaseEndpoint Target => new(DatabaseProvider.Sqlite, $"Data Source={_targetPath};Pooling=False");

    private sealed class RecordingProgress : IProgress<ConversionProgress>
    {
        public List<ConversionProgress> Reports { get; } = [];
        public void Report(ConversionProgress value) => Reports.Add(value);
    }

    [SetUp]
    public async Task SetUpTarget()
    {
        _targetPath = Path.Combine(Path.GetTempPath(), $"grif-infrastructure-copy-{Guid.NewGuid():N}.db");
        await using var target = TargetContext();
        await target.Database.EnsureCreatedAsync();
    }

    [TearDown]
    public void TearDownTarget() => SqliteFiles.Delete(_targetPath);

    private GrifballContext TargetContext() =>
        new(new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(DatabaseProvider.Sqlite, Target.ConnectionString).Options);

    /// <summary>Three users (two renamed since), a season, and a team drafted with its first user as captain: the users,
    /// and the captain's roster row (Team.CaptainID).</summary>
    private async Task<(List<User> Users, int? CaptainID)> SeedSourceAsync()
    {
        var users = new List<User>
        {
            User.Seed(userName: "captain", displayName: "Captain"),
            User.Seed(userName: "second", displayName: "Second"),
            User.Seed(userName: "third", displayName: "Third"),
        };
        Context.Users.AddRange(users);
        var season = Season.Seed(seasonName: "Copied Season");
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var team = Team.Drafted(season.SeasonID, "Copied Team", users[0].Id, 1);
        Context.Teams.Add(team);
        await Context.SaveChangesAsync();
        Assert.That(team.SetCaptain(users[0].Id), Is.EqualTo(TeamCaptainResult.Appointed));
        users[1].Rename("Second, renamed");
        users[2].Rename("Third, renamed");
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return (users, team.CaptainID);
    }

    private async Task<IReadOnlyList<ConvertedTable>> CopyAsync(IProgress<ConversionProgress>? progress = null)
    {
        await using var target = TargetContext();
        return await _converter.CopyAsync(Source, Target, ConversionTables.Order(target), CancellationToken.None, progress);
    }

    [Test]
    public async Task Copy_PutsEveryRowInTheTarget_CaptainsCycleIncluded()
    {
        var (users, captainId) = await SeedSourceAsync();

        var copied = await CopyAsync();

        await using var target = TargetContext();
        var copiedUsers = await target.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => new { u.Id, u.UserName, u.DisplayName }).ToListAsync();
        var team = await target.Teams.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(copiedUsers.Select(u => (u.Id, u.UserName, u.DisplayName)),
                Is.EqualTo(users.Select(u => (u.Id, (string?)u.UserName, (string?)u.DisplayName))));
            Assert.That((team.TeamName, team.CaptainID), Is.EqualTo(("Copied Team", captainId)), "the deferred captain, set once its player was in");
            Assert.That(captainId, Is.GreaterThan(0), "the seeded team has its captain");
            Assert.That(copied.Single(t => t.Name == "Users").Rows, Is.EqualTo(3));
            Assert.That(copied.Single(t => t.Name == "Teams").Rows, Is.EqualTo(1));
        }
        Assert.That(await _converter.CompareAsync(Source, Target), Is.Empty, "the copy reads back as the source");
        var logged = _log.At(Microsoft.Extensions.Logging.LogLevel.Information);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(logged, Has.One.StartsWith("Set the deferred foreign keys of 1 rows of "));
            Assert.That(logged[^1], Does.Match(@"^Compared \d+ tables; 0 differ$"));
        }
    }

    [Test]
    public async Task Copy_ReportsEachTableCopied_ThenEachChecked()
    {
        await SeedSourceAsync();
        var progress = new RecordingProgress();

        var copied = await CopyAsync(progress);

        var steps = copied.Count * 2;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(progress.Reports, Has.Count.EqualTo(steps));
            Assert.That(progress.Reports.Select(r => r.Done), Is.EqualTo(Enumerable.Range(0, steps)));
            Assert.That(progress.Reports.Select(r => r.Total), Is.All.EqualTo(steps));
            Assert.That(progress.Reports[0].Message, Does.StartWith("Copying ").And.EndWith($"(1 of {copied.Count})"));
            Assert.That(progress.Reports[^1].Message, Is.EqualTo($"Checking the copy ({copied.Count} of {copied.Count})"));
        }
    }

    [Test]
    public async Task Compare_ATargetChangedSince_NamesTheTablesThatDiffer()
    {
        var (users, _) = await SeedSourceAsync();
        await CopyAsync();
        await using (var target = TargetContext())
        {
            var user = await target.Users.SingleAsync(u => u.Id == users[2].Id);
            target.Users.Remove(user);
            await target.SaveChangesAsync();
        }

        var differing = await _converter.CompareAsync(Source, Target);

        Assert.That(differing, Does.Contain("Auth.Users (3 rows in the source, 2 in the target)"));
    }
}
