using DotNet.Testcontainers.Containers;
using GrifballWebApp.Convert;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Conversion;
using GrifballWebApp.Database.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace GrifballWebApp.Test;

/// <summary>
/// DatabaseConverter between the suite's server and the other kind, started here, so each CI run
/// converts its own server's database into the other's and back (and onto its own kind).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DatabaseConversionTests
{
    // The other server, one for the fixture; each test its own databases on both.
    private static IDatabaseContainer _other = null!;
    private readonly List<DatabaseEndpoint> _databases = [];

    [OneTimeSetUp]
    public static async Task StartOtherServer() => _other = await TestDatabase.StartServer(TestDatabase.Other);

    [OneTimeTearDown]
    public static async Task StopOtherServer() => await _other.DisposeAsync();

    [TearDown]
    public async Task DropDatabases()
    {
        foreach (var database in _databases)
        {
            if (database.Provider == DatabaseProvider.Postgres)
                NpgsqlConnection.ClearPool(new NpgsqlConnection(database.ConnectionString));
            else
                SqlConnection.ClearPool(new SqlConnection(database.ConnectionString));
            await using var context = DatabaseConverter.NewContext(database);
            await context.Database.EnsureDeletedAsync();
        }
        _databases.Clear();
    }

    private DatabaseEndpoint Database(DatabaseProvider provider)
    {
        var server = provider == TestDatabase.Provider ? SetUpFixture.DatabaseContainer : _other;
        var endpoint = new DatabaseEndpoint(provider, TestDatabase.ConnectionString(server, $"Conv_{Guid.NewGuid():N}", provider));
        _databases.Add(endpoint);
        return endpoint;
    }

    private static DatabaseConverter Converter(ILogger<DatabaseConverter>? logger = null) => new(logger ?? NullLogger<DatabaseConverter>.Instance);

    private static async Task<bool> ExistsAsync(DatabaseEndpoint database)
    {
        await using var context = DatabaseConverter.NewContext(database);
        return await context.GetService<IRelationalDatabaseCreator>().ExistsAsync();
    }

    // SQL Server's 100ns, which Postgres keeps to the microsecond.
    private static readonly DateTime Precise = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc).AddTicks(1_234_567);

    private sealed record Filled(int SeasonID, DateTime WhileFirst, int FinalID, int SemiHomeID, int SemiAwayID, int TeamID, int CaptainID,
        int RescheduleID, int SeasonMatchID, int LockedUserID, int CreatorUserID, Guid MatchID);

    private static async Task<DateTime> PeriodStartOf(GrifballContext db, Season season)
        => await db.Seasons.Where(s => s.SeasonID == season.SeasonID).Select(s => EF.Property<DateTime>(s, "PeriodStart")).SingleAsync();

    /// <summary>
    /// A database the app could have written: rows in most tables, with history (updates and deletes);
    /// rows referring to later rows of their own table (a bracket's final before its semifinals); both
    /// cycles between tables (a team and its captain, a match and its active reschedule); and the types
    /// that differ between the servers (datetimeoffset, the end of time, durations, ulong, bytes, a time
    /// of day, case and accents in text).
    /// </summary>
    private static async Task<Filled> Fill(DatabaseEndpoint database)
    {
        await using var db = DatabaseConverter.NewContext(database);
        await db.Database.MigrateAsync();

        var creator = new User
        {
            UserName = "creator", NormalizedUserName = "CREATOR", Email = "creator@example.com", CreatedAt = Precise, ModifiedAt = Precise,
            // Npgsql writes only UTC offsets; SQL Server keeps any, which Postgres holds as the same instant.
            LockoutEnd = database.Provider == DatabaseProvider.SqlServer
                ? new DateTimeOffset(2026, 11, 1, 8, 30, 0, TimeSpan.FromHours(-5))
                : new DateTimeOffset(2026, 11, 1, 13, 30, 0, TimeSpan.Zero),
            XboxUser = new XboxUser { XboxUserID = 2535405290000001, Gamertag = "Noé O'Brien" },
        };
        var locked = new User
        {
            UserName = "locked", NormalizedUserName = "LOCKED", CreatedAt = Precise, ModifiedAt = Precise,
            LockoutEnd = DateTimeOffset.MaxValue,
            XboxUser = new XboxUser { XboxUserID = 2535405290000002, Gamertag = "ALPHA bravo" },
            DiscordUser = new DiscordUser { DiscordUserID = 123456789012345678, DiscordUsername = "noah" },
        };
        db.Users.AddRange(creator, locked);
        await db.SaveChangesAsync();
        creator.CreatedByID = locked.Id;
        await db.SaveChangesAsync();

        var season = new Season
        {
            SeasonName = "First", PublicAt = Precise, SignupsOpen = Precise, SignupsClose = Precise.AddDays(7),
            DraftStart = Precise.AddDays(8), SeasonStart = Precise.AddDays(14), SeasonEnd = Precise.AddDays(60),
        };
        db.Seasons.Add(season);
        await db.SaveChangesAsync();
        var first = await PeriodStartOf(db, season);
        await Task.Delay(30);
        season.SeasonName = "Second";
        await db.SaveChangesAsync();
        var second = await PeriodStartOf(db, season);
        await Task.Delay(30);
        season.SeasonName = "Third";
        await db.SaveChangesAsync();

        var team = new Team { Season = season, TeamName = "Équipe ü" };
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        var captain = new TeamPlayer { Team = team, UserID = creator.Id, DraftRound = 1, DraftPick = 1 };
        var traded = new TeamPlayer { Team = team, UserID = locked.Id };
        db.TeamPlayers.AddRange(captain, traded);
        await db.SaveChangesAsync();
        team.CaptainID = captain.TeamPlayerID;
        db.TeamPlayers.Remove(traded);
        await db.SaveChangesAsync();

        var finalMatch = new SeasonMatch { Season = season, BestOf = 5, ScheduledTime = Precise };
        var semiHomeMatch = new SeasonMatch { Season = season, BestOf = 3, HomeTeam = team };
        var semiAwayMatch = new SeasonMatch { Season = season, BestOf = 3 };
        db.SeasonMatches.AddRange(finalMatch, semiHomeMatch, semiAwayMatch);
        await db.SaveChangesAsync();
        var final = new MatchBracketInfo { SeasonMatch = finalMatch, RoundNumber = 2, MatchNumber = 1, HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 2 };
        db.MatchBracketInfo.Add(final);
        await db.SaveChangesAsync();
        var semiHome = new MatchBracketInfo { SeasonMatch = semiHomeMatch, RoundNumber = 1, MatchNumber = 1, HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 4 };
        var semiAway = new MatchBracketInfo { SeasonMatch = semiAwayMatch, RoundNumber = 1, MatchNumber = 2, HomeTeamSeedNumber = 2, AwayTeamSeedNumber = 3 };
        db.MatchBracketInfo.AddRange(semiHome, semiAway);
        await db.SaveChangesAsync();
        final.HomeTeamSeedNumber = null;
        final.HomeTeamPreviousMatchBracketInfoID = semiHome.MatchBracketInfoID;
        final.AwayTeamSeedNumber = null;
        final.AwayTeamPreviousMatchBracketInfoID = semiAway.MatchBracketInfoID;
        await db.SaveChangesAsync();

        var reschedule = new MatchReschedule
        {
            SeasonMatch = finalMatch, Reason = "Busy", CommissionerNotes = "", RequestedByUserID = creator.Id, RequestedAt = Precise,
            OriginalScheduledTime = Precise, DiscordThreadID = ulong.MaxValue,
        };
        db.MatchReschedules.Add(reschedule);
        await db.SaveChangesAsync();
        finalMatch.ActiveRescheduleRequestId = reschedule.MatchRescheduleID;
        await db.SaveChangesAsync();

        var match = new Match { MatchID = Guid.NewGuid(), StartTime = Precise, Duration = TimeSpan.FromTicks(7_123_456_789) };
        db.Matches.Add(match);
        db.MatchTeams.Add(new MatchTeam { Match = match, TeamID = 0, Score = 50 });
        await db.SaveChangesAsync();
        db.MatchParticipants.Add(new MatchParticipant
        {
            MatchID = match.MatchID, TeamID = 0, XboxUserID = 2535405290000001, Kda = 1.25f, Accuracy = 33.33333f,
            AverageLife = TimeSpan.FromTicks(301_234_567), TimePlayed = TimeSpan.FromMinutes(12), FirstJoinedTime = Precise,
        });
        db.MatchedMatches.Add(new MatchedMatch
        {
            HomeTeam = new MatchedTeam(), AwayTeam = new MatchedTeam(), StartedAt = Precise, MatchID = match.MatchID,
            ThreadID = ulong.MaxValue, VoteMessageID = 1,
        });
        db.Ranks.Add(new Rank { Name = "Onyx", MmrThreshold = 1500, Color = "#101010", Description = "Top", Icon = [0, 1, 2, 254, 255] });
        db.Availability.Add(new AvailabilityOption { DayOfWeek = DayOfWeek.Friday, Time = new TimeOnly(19, 30, 15) });
        db.DataProtectionKeys.Add(new DataProtectionKey { FriendlyName = "key-1", Xml = "<key id=\"1\"/>" });
        db.QueuedPlayer.Add(new QueuedPlayer { UserID = locked.Id, JoinedAt = Precise });
        await db.SaveChangesAsync();
        db.QueuedPlayer.Remove(await db.QueuedPlayer.SingleAsync());
        // A row the migrations seeded, changed: the source's version is the one to keep.
        var region = await db.Regions.OrderBy(r => r.RegionID).FirstAsync();
        region.RegionName += " (edited)";
        await db.SaveChangesAsync();

        return new Filled(season.SeasonID, first + (second - first) / 2, final.MatchBracketInfoID, semiHome.MatchBracketInfoID, semiAway.MatchBracketInfoID,
            team.TeamID, captain.TeamPlayerID, reschedule.MatchRescheduleID, finalMatch.SeasonMatchID, locked.Id, creator.Id, match.MatchID);
    }

    /// <summary>Every table's fingerprint, its columns taken by name: as one server or the other holds it.</summary>
    private static async Task<Dictionary<string, TableFingerprint>> Fingerprints(DatabaseEndpoint database)
    {
        var dialect = SqlDialect.For(database.Provider);
        await using var connection = database.Provider == DatabaseProvider.Postgres
            ? (System.Data.Common.DbConnection)new NpgsqlConnection(database.ConnectionString)
            : new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var result = new Dictionary<string, TableFingerprint>();
        foreach (var (table, columns) in await dialect.ReadColumnsAsync(connection, null, CancellationToken.None))
        {
            var names = columns.Select(c => c.Name).Order(StringComparer.Ordinal).ToList();
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {string.Join(", ", names.Select(dialect.Quote))} FROM {dialect.Quote(table)}";
            await using var reader = await command.ExecuteReaderAsync();
            var rows = new List<object?[]>();
            while (await reader.ReadAsync())
                rows.Add(Enumerable.Range(0, names.Count).Select(i => reader.IsDBNull(i) ? null : reader.GetValue(i)).ToArray());
            result[table.ToString()] = TableFingerprint.Of(rows);
        }
        return result;
    }

    /// <summary>
    /// What keeps the database's history: SQL Server's system-versioned tables and their history
    /// tables, or Postgres's row history triggers and whether each is on.
    /// </summary>
    private static async Task<string[]> Versioned(DatabaseEndpoint database)
    {
        await using var db = DatabaseConverter.NewContext(database);
        var sql = database.Provider == DatabaseProvider.SqlServer
            ? "SELECT SCHEMA_NAME(t.schema_id) + '.' + t.name + ' -> ' + OBJECT_NAME(t.history_table_id) AS Value FROM sys.tables t WHERE t.temporal_type = 2"
            : "SELECT n.nspname || '.' || c.relname || '.' || t.tgname || ' ' || t.tgenabled::text AS \"Value\" FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace WHERE NOT t.tgisinternal";
        return [.. (await db.Database.SqlQueryRaw<string>(sql).ToListAsync()).Order(StringComparer.Ordinal)];
    }

    [Test]
    public async Task Converts_EveryRowAndItsHistory_ToTheOtherServerAndBack()
    {
        var source = Database(TestDatabase.Provider);
        var filled = await Fill(source);
        var target = Database(TestDatabase.Other);

        var converted = await Converter().ConvertAsync(source, target);

        var sourcePrints = await Fingerprints(source);
        var targetPrints = await Fingerprints(target);
        Assert.Multiple(() =>
        {
            Assert.That(converted.Select(t => new TableName(t.Schema, t.Name).ToString()), Is.EquivalentTo(sourcePrints.Keys));
            Assert.That(targetPrints, Is.EquivalentTo(sourcePrints), "every table's rows, to the microsecond");
            Assert.That(converted.Sum(t => t.Rows), Is.GreaterThan(40));
        });

        // Every table keeps its history as a freshly migrated database's does.
        var fresh = Database(TestDatabase.Other);
        await using (var db = DatabaseConverter.NewContext(fresh))
            await db.Database.MigrateAsync();
        var versioned = await Versioned(target);
        var freshVersioned = await Versioned(fresh);
        Assert.Multiple(() =>
        {
            Assert.That(versioned, Has.Length.GreaterThanOrEqualTo(39));
            Assert.That(versioned, Is.EqualTo(freshVersioned));
        });

        await using (var db = DatabaseConverter.NewContext(target))
        {
            var asOf = await db.Seasons.TemporalAsOf(filled.WhileFirst).Where(s => s.SeasonID == filled.SeasonID).Select(s => s.SeasonName).SingleAsync();
            var versions = await db.Seasons.TemporalAll().Where(s => s.SeasonID == filled.SeasonID)
                .OrderBy(s => EF.Property<DateTime>(s, "PeriodStart")).Select(s => s.SeasonName).ToListAsync();
            var final = await db.MatchBracketInfo.SingleAsync(b => b.MatchBracketInfoID == filled.FinalID);
            var team = await db.Teams.SingleAsync(t => t.TeamID == filled.TeamID);
            var seasonMatch = await db.SeasonMatches.SingleAsync(m => m.SeasonMatchID == filled.SeasonMatchID);
            var locked = await db.Users.SingleAsync(u => u.Id == filled.LockedUserID);
            var creator = await db.Users.SingleAsync(u => u.Id == filled.CreatorUserID);
            var reschedule = await db.MatchReschedules.SingleAsync(r => r.MatchRescheduleID == filled.RescheduleID);
            var match = await db.Matches.SingleAsync(m => m.MatchID == filled.MatchID);
            var rank = await db.Ranks.SingleAsync(r => r.Name == "Onyx");
            var availability = await db.Availability.SingleAsync(a => a.DayOfWeek == DayOfWeek.Friday);
            var gamertag = await db.XboxUsers.Where(x => x.Gamertag == "noé o'brien").Select(x => x.Gamertag).SingleAsync();
            var traded = await db.TeamPlayers.TemporalAll().CountAsync(p => p.TeamID == filled.TeamID);
            Assert.Multiple(() =>
            {
                Assert.That(asOf, Is.EqualTo("First"));
                Assert.That(versions, Is.EqualTo(new[] { "First", "Second", "Third" }));
                Assert.That(final.HomeTeamPreviousMatchBracketInfoID, Is.EqualTo(filled.SemiHomeID));
                Assert.That(final.AwayTeamPreviousMatchBracketInfoID, Is.EqualTo(filled.SemiAwayID));
                Assert.That(team.CaptainID, Is.EqualTo(filled.CaptainID));
                Assert.That(seasonMatch.ActiveRescheduleRequestId, Is.EqualTo(filled.RescheduleID));
                Assert.That(locked.LockoutEnd, Is.EqualTo(DateTimeOffset.MaxValue));
                Assert.That(creator.LockoutEnd, Is.EqualTo(new DateTimeOffset(2026, 11, 1, 13, 30, 0, TimeSpan.Zero)));
                Assert.That(creator.CreatedAt, Is.EqualTo(Precise).Within(TimeSpan.FromTicks(10)));
                Assert.That(reschedule.DiscordThreadID, Is.EqualTo(ulong.MaxValue));
                Assert.That(match.Duration, Is.EqualTo(TimeSpan.FromTicks(7_123_456_789)).Within(TimeSpan.FromTicks(10)));
                Assert.That(rank.Icon, Is.EqualTo(new byte[] { 0, 1, 2, 254, 255 }));
                Assert.That(availability.Time, Is.EqualTo(new TimeOnly(19, 30, 15)));
                Assert.That(gamertag, Is.EqualTo("Noé O'Brien"), "text compares case-insensitively on both");
                Assert.That(traded, Is.EqualTo(2), "the deleted player's history");
            });

            // The converted database carries on as one the app made: new keys after the copied ones,
            // and history for the changes made to it.
            var maxSeasonID = await db.Seasons.MaxAsync(s => s.SeasonID);
            var season = new Season
            {
                SeasonName = "After", PublicAt = Precise, SignupsOpen = Precise, SignupsClose = Precise,
                DraftStart = Precise, SeasonStart = Precise, SeasonEnd = Precise,
            };
            db.Seasons.Add(season);
            await db.SaveChangesAsync();
            season.SeasonName = "After, renamed";
            await db.SaveChangesAsync();
            var afterVersions = await db.Seasons.TemporalAll().CountAsync(s => s.SeasonID == season.SeasonID);
            Assert.Multiple(() =>
            {
                Assert.That(season.SeasonID, Is.GreaterThan(maxSeasonID));
                Assert.That(afterVersions, Is.EqualTo(2));
            });
        }

        // And back onto the suite's own server: the same rows as the database it started from.
        var back = Database(TestDatabase.Provider);
        await Converter().ConvertAsync(target, back);
        var backPrints = await Fingerprints(back);
        var targetNow = await Fingerprints(target);
        Assert.That(backPrints, Is.EquivalentTo(targetNow));
    }

    [Test]
    public async Task Converts_OntoTheSameKindOfServer_FromASnapshotWhereThereIsOne()
    {
        var source = Database(TestDatabase.Provider);
        await Fill(source);
        if (TestDatabase.Provider == DatabaseProvider.SqlServer)
        {
            await using var db = DatabaseConverter.NewContext(source);
            await db.Database.ExecuteSqlRawAsync($"ALTER DATABASE [{db.Database.GetDbConnection().Database}] SET ALLOW_SNAPSHOT_ISOLATION ON");
        }
        var target = Database(TestDatabase.Provider);

        await Converter().ConvertAsync(source, target);

        Assert.That(await Fingerprints(target), Is.EquivalentTo(await Fingerprints(source)));
    }

    [Test]
    public async Task Refuses_ATargetThatHasTables_AndLeavesIt()
    {
        var source = Database(TestDatabase.Provider);
        await Fill(source);
        var target = Database(TestDatabase.Other);
        await using (var db = DatabaseConverter.NewContext(target))
            await db.Database.MigrateAsync();

        var e = Assert.ThrowsAsync<ConversionException>(() => Converter().ConvertAsync(source, target));

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(e!.Message, Does.Contain("already has tables"));
            Assert.That(await ExistsAsync(target), Is.True);
        });
    }

    [Test]
    public async Task Refuses_ASourceWithoutEveryMigration_BeforeMakingTheTarget()
    {
        var source = Database(TestDatabase.Provider);
        await using (var db = DatabaseConverter.NewContext(source))
            await db.GetService<IMigrator>().MigrateAsync(db.Database.GetMigrations().First());
        var target = Database(TestDatabase.Other);

        var e = Assert.ThrowsAsync<ConversionException>(() => Converter().ConvertAsync(source, target));

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(e!.Message, Does.Contain("lacks migrations of this build"));
            Assert.That(await ExistsAsync(target), Is.False);
        });
    }

    [Test]
    public async Task Refuses_ASourceWithMigrationsThisBuildDoesNotHave()
    {
        var source = Database(TestDatabase.Provider);
        await using (var db = DatabaseConverter.NewContext(source))
        {
            await db.Database.MigrateAsync();
            var history = db.GetService<IHistoryRepository>();
            await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow("29991231000000_FromTheFuture", "10.0.12")));
        }

        var e = Assert.ThrowsAsync<ConversionException>(() => Converter().ConvertAsync(source, Database(TestDatabase.Other)));

        Assert.That(e!.Message, Does.Contain("29991231000000_FromTheFuture"));
    }

    [Test]
    public void Refuses_ASourceThatDoesNotExist()
    {
        var e = Assert.ThrowsAsync<ConversionException>(() => Converter().ConvertAsync(Database(TestDatabase.Provider), Database(TestDatabase.Other)));

        Assert.That(e!.Message, Does.Contain("doesn't exist"));
    }

    /// <summary>Cancels the conversion as it logs that it copied its first table.</summary>
    private sealed class CancelAfterFirstTable(CancellationTokenSource cancellation) : ILogger<DatabaseConverter>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter(state, exception).StartsWith("Copied"))
                cancellation.Cancel();
        }
    }

    [Test]
    public async Task DropsTheTargetItCreated_WhenTheCopyFails()
    {
        var source = Database(TestDatabase.Provider);
        await Fill(source);
        var target = Database(TestDatabase.Other);
        using var cancellation = new CancellationTokenSource();

        Assert.CatchAsync<OperationCanceledException>(() => new DatabaseConverter(new CancelAfterFirstTable(cancellation)).ConvertAsync(source, target, cancellation.Token));

        Assert.That(await ExistsAsync(target), Is.False);
    }

    /// <summary>A database with no tables, as a managed server's is often made beforehand.</summary>
    private static async Task CreateEmpty(DatabaseEndpoint database)
    {
        await using var db = DatabaseConverter.NewContext(database);
        await db.GetService<IRelationalDatabaseCreator>().CreateAsync();
    }

    private static async Task<bool> HasTablesAsync(DatabaseEndpoint database)
    {
        await using var db = DatabaseConverter.NewContext(database);
        return await db.GetService<IRelationalDatabaseCreator>().HasTablesAsync();
    }

    [Test]
    public async Task Converts_IntoAnEmptyDatabaseMadeBeforehand()
    {
        var source = Database(TestDatabase.Provider);
        await Fill(source);
        var target = Database(TestDatabase.Other);
        await CreateEmpty(target);

        await Converter().ConvertAsync(source, target);

        Assert.That(await Fingerprints(target), Is.EquivalentTo(await Fingerprints(source)));
    }

    [Test]
    public async Task LeavesAnEmptyDatabaseMadeBeforehandEmpty_WhenTheCopyFails_SoTheNextTryTakesIt()
    {
        var source = Database(TestDatabase.Provider);
        await Fill(source);
        var target = Database(TestDatabase.Other);
        await CreateEmpty(target);
        using var cancellation = new CancellationTokenSource();

        Assert.CatchAsync<OperationCanceledException>(() => new DatabaseConverter(new CancelAfterFirstTable(cancellation)).ConvertAsync(source, target, cancellation.Token));

        var exists = await ExistsAsync(target);
        var hasTables = await HasTablesAsync(target);
        Assert.Multiple(() =>
        {
            Assert.That(exists, Is.True, "a database made beforehand stays");
            Assert.That(hasTables, Is.False, "with no tables, not the migrations' empty ones");
        });

        await Converter().ConvertAsync(source, target);
        Assert.That(await Fingerprints(target), Is.EquivalentTo(await Fingerprints(source)));
    }

    [Test]
    public async Task Command_ConvertsIntoTheAppsOwnDatabase_ByDefault()
    {
        var source = Database(TestDatabase.Provider);
        await Fill(source);
        var target = Database(TestDatabase.Other);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Source:Provider"] = source.Provider.ToString().ToLowerInvariant(),
            ["Source:ConnectionString"] = source.ConnectionString,
            ["Database:Provider"] = target.Provider.ToString(),
            ["ConnectionStrings:GrifballWebApp"] = target.ConnectionString,
        }).Build();

        var exit = await ConvertCommand.RunAsync(configuration, NullLoggerFactory.Instance);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(exit, Is.EqualTo(0));
            Assert.That(await Fingerprints(target), Is.EquivalentTo(await Fingerprints(source)));
        });
    }

    [Test]
    public async Task Command_Fails_WhenTheConversionRefuses()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Source:Provider"] = TestDatabase.Provider.ToString(),
            ["Source:ConnectionString"] = Database(TestDatabase.Provider).ConnectionString,
            ["Target:Provider"] = TestDatabase.Other.ToString(),
            ["Target:ConnectionString"] = Database(TestDatabase.Other).ConnectionString,
        }).Build();

        Assert.That(await ConvertCommand.RunAsync(configuration, NullLoggerFactory.Instance), Is.EqualTo(1));
    }

    [Test]
    public async Task Command_Fails_WhenTheSourceCanNotBeReached()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Source:Provider"] = TestDatabase.Provider.ToString(),
            ["Source:ConnectionString"] = TestDatabase.UnreachableConnectionString(),
            ["Target:Provider"] = TestDatabase.Other.ToString(),
            ["Target:ConnectionString"] = Database(TestDatabase.Other).ConnectionString,
        }).Build();

        Assert.That(await ConvertCommand.RunAsync(configuration, NullLoggerFactory.Instance), Is.EqualTo(1));
    }

    [Test]
    public async Task Command_TakesItsSettingsFromTheCommandLine()
    {
        string[] args = ["--Source:Provider=Oracle", "--Source:ConnectionString=x", "--Target:Provider=Postgres", "--Target:ConnectionString=x"];

        Assert.That(await ConvertCommand.MainAsync(args), Is.EqualTo(2));
    }

    [TestCase(null, "x", "Postgres", "x", TestName = "Command_NeedsTheSourceProvider")]
    [TestCase("Oracle", "x", "Postgres", "x", TestName = "Command_NeedsAKnownProvider")]
    [TestCase("SqlServer", null, "Postgres", "x", TestName = "Command_NeedsTheSourceConnectionString")]
    [TestCase("SqlServer", "x", null, "x", TestName = "Command_NeedsTheTargetProvider")]
    [TestCase("SqlServer", "x", "Postgres", null, TestName = "Command_NeedsTheTargetConnectionString")]
    public async Task Command_Settings(string? sourceProvider, string? sourceConnection, string? targetProvider, string? targetConnection)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Source:Provider"] = sourceProvider,
            ["Source:ConnectionString"] = sourceConnection,
            ["Target:Provider"] = targetProvider,
            ["Target:ConnectionString"] = targetConnection,
        }).Build();

        Assert.That(await ConvertCommand.RunAsync(configuration, NullLoggerFactory.Instance), Is.EqualTo(2));
    }
}
