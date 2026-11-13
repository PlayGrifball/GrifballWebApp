using DiscordInterface.Generated;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Infrastructure.Matchmaking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// The named locks (DatabaseLocks) and a season's row lock (ContextDatabaseLocks) on SQL Server and PostgreSQL: a
/// GrifballContext on each provider over a fake connection (FakeDbConnection), which records what is sent and answers
/// as the server would. Taken at once or after someone else lets go, released, and given up on an error or a
/// cancellation, the connection closed again each time. No server.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ServerLocksUnitTests
{
    private const string Name = "season-draft-7";
    private static readonly string[] AdvisoryLockAndUnlock = ["SELECT CASE WHEN pg_try_advisory_lock(@lock) THEN 0 ELSE -1 END", "SELECT pg_advisory_unlock(@lock)"];
    private static readonly object?[] SeasonSeven = [7];
    private readonly FakeDbConnection _connection = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero));

    [TearDown]
    public void TearDown() => _connection.Dispose();

    private GrifballContext SqlServer() => new(new DbContextOptionsBuilder<GrifballContext>().UseSqlServer(_connection).Options);

    private GrifballContext Postgres() => new(new DbContextOptionsBuilder<GrifballContext>().UseNpgsql(_connection).Options);

    /// <summary>The lock someone else holds for the first <paramref name="refusals"/> tries (-1), then granted (0).</summary>
    private void Held(int refusals)
    {
        var tries = 0;
        _connection.Answer = sent => sent.Sql.Contains("sp_getapplock", StringComparison.Ordinal) || sent.Sql.Contains("pg_try_advisory_lock", StringComparison.Ordinal)
            ? (++tries <= refusals ? -1 : 0)
            : 0;
    }

    [Test]
    public async Task SqlServer_TakesAnApplicationLock_TryingAgainWhileItsHeld_AndReleasesIt()
    {
        Held(refusals: 1);
        await using var context = SqlServer();

        var acquiring = context.Database.AcquireLockAsync(Name, _time, CancellationToken.None);
        // Refused once: it waits DatabaseLocks.RetryEvery on the clock, then tries again
        for (var i = 0; i < 100 && !acquiring.IsCompleted; i++)
        {
            _time.Advance(DatabaseLocks.RetryEvery);
            await Task.Yield();
        }
        var handle = await acquiring.WaitAsync(TimeSpan.FromSeconds(30));
        var whileHeld = (_connection.Closed, _connection.State);
        await handle.DisposeAsync();
        await handle.DisposeAsync();

        var sent = _connection.Sent;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent.Count(c => c.Sql.Contains("EXEC @result = sp_getapplock @Resource = @lock, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0", StringComparison.Ordinal)),
                Is.EqualTo(2), "refused once, then taken");
            Assert.That(sent[^1].Sql, Is.EqualTo("EXEC sp_releaseapplock @Resource = @lock, @LockOwner = 'Session'"), "released once, however often disposed");
            Assert.That(sent.Select(c => c.Parameters["@lock"]), Is.All.EqualTo(Name));
            Assert.That(whileHeld, Is.EqualTo((0, System.Data.ConnectionState.Open)), "the session holds the lock: its connection stays open");
            Assert.That((_connection.Opened, _connection.Closed), Is.EqualTo((1, 1)));
        }
    }

    [Test]
    public async Task Postgres_TakesAnAdvisoryLock_ByTheNamesKey_AndReleasesIt()
    {
        Held(refusals: 0);
        await using var context = Postgres();

        await (await context.Database.AcquireLockAsync(Name, _time, CancellationToken.None)).DisposeAsync();

        var sent = _connection.Sent;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent.Select(c => c.Sql), Is.EqualTo(AdvisoryLockAndUnlock));
            Assert.That(sent.Select(c => c.Parameters["@lock"]), Is.All.EqualTo(DatabaseLocks.Key(Name)));
            Assert.That(_connection.Closed, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task AnyErrorTakingTheLock_ClosesTheConnection_AndIsTheCallers()
    {
        _connection.Answer = _ => throw new InvalidOperationException("deadlock victim");
        await using var context = SqlServer();

        using (Assert.EnterMultipleScope())
        {
            await Assert.ThatAsync(() => context.Database.AcquireLockAsync(Name, _time, CancellationToken.None), Throws.InvalidOperationException.With.Message.EqualTo("deadlock victim"));
            Assert.That(_connection.Closed, Is.EqualTo(1));
        }
    }

    /// <summary>sp_getapplock's errors (-2 cancelled, -3 deadlock, -999 bad call) are turned into an error on the server, by
    /// the SQL sent: C# reads only "granted" (0 or more) or "held" (-1), so without this guard an error would be retried
    /// forever.</summary>
    [Test]
    public async Task SqlServer_TheLocksErrorsAreThrownByTheServer_NotRetried()
    {
        Held(refusals: 0);
        await using var context = SqlServer();

        await (await context.Database.AcquireLockAsync(Name, _time, CancellationToken.None)).DisposeAsync();

        Assert.That(_connection.Sent[0].Sql, Does.Contain("IF @result < -1 THROW 50000, 'sp_getapplock refused the lock', 1;"));
    }

    [Test]
    public async Task ACancelledWait_StopsTrying_AndClosesTheConnection()
    {
        using var stop = new CancellationTokenSource();
        _connection.Answer = _ =>
        {
            // Held by someone else, and the caller gives up while it waits to try again
            stop.Cancel();
            return -1;
        };
        await using var context = SqlServer();

        using (Assert.EnterMultipleScope())
        {
            await Assert.ThatAsync(() => context.Database.AcquireLockAsync(Name, _time, stop.Token), Throws.InstanceOf<OperationCanceledException>());
            Assert.That((_connection.Sent.Count, _connection.Closed), Is.EqualTo((1, 1)));
        }
    }

    [Test]
    public async Task ANullClock_IsRefused_BeforeTheConnectionIsOpened()
    {
        Held(refusals: 0);
        await using var context = SqlServer();

        using (Assert.EnterMultipleScope())
        {
            await Assert.ThatAsync(() => context.Database.AcquireLockAsync(Name, null!, CancellationToken.None),
                Throws.ArgumentNullException.With.Property(nameof(ArgumentNullException.ParamName)).EqualTo("time"));
            Assert.That((_connection.Opened, _connection.Sent.Count), Is.EqualTo((0, 0)));
        }
    }

    /// <summary>QueueService.Go waits for the queue's lock on its own clock: held by another worker, the next try comes
    /// only when that clock has moved on. Past the lock, the queue read fails here, so the tick goes no further.</summary>
    [Test]
    public async Task QueueService_WaitsForTheQueueLock_OnItsOwnClock()
    {
        Held(refusals: 1);
        await using var context = SqlServer();
        var queue = Substitute.For<IQueueRepository>();
        queue.GetQueuePlayersWithInfo(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("past the lock"));
        var service = new QueueService(NullLogger<QueueService>.Instance, Options.Create(new DiscordOptions { QueueChannel = 111UL }), queue,
            Substitute.For<IDiscordRestClient>(), context, Substitute.For<IDataPullService>(), Substitute.For<IOutbox>(), Substitute.For<IQueueNotifier>(), _time);
        int Tries() => _connection.Sent.Count(c => c.Sql.Contains("sp_getapplock", StringComparison.Ordinal));

        var going = service.Go(CancellationToken.None);
        for (var i = 0; i < 100 && Tries() < 1; i++)
            await Task.Yield();
        var beforeTheClockMoved = (Tries(), going.IsCompleted);
        for (var i = 0; i < 100 && !going.IsCompleted; i++)
        {
            _time.Advance(DatabaseLocks.RetryEvery);
            await Task.Yield();
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(beforeTheClockMoved, Is.EqualTo((1, false)), "refused once, and waiting on the service's clock");
            await Assert.ThatAsync(() => going.WaitAsync(TimeSpan.FromSeconds(30)), Throws.InvalidOperationException.With.Message.EqualTo("past the lock"));
            Assert.That(Tries(), Is.EqualTo(2), "tried again once the clock moved on");
            Assert.That(_connection.Sent[^1].Sql, Is.EqualTo("EXEC sp_releaseapplock @Resource = @lock, @LockOwner = 'Session'"), "and let go after the failed tick");
            Assert.That(_connection.Sent.Select(c => c.Parameters["@lock"]), Is.All.EqualTo(QueueService.LockName));
        }
    }

    [Test]
    public async Task SeasonRowLock_OnSqlServer_IsAnUpdateLockHeldToTheTransactionsEnd()
    {
        await using var context = SqlServer();

        await new ContextDatabaseLocks(context).LockSeasonRowAsync(7);

        var sent = _connection.Sent.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent.Sql, Does.StartWith("SELECT 1 FROM [Event].[Seasons] WITH (UPDLOCK, HOLDLOCK) WHERE [SeasonID] = @"));
            Assert.That(sent.Parameters.Values, Is.EqualTo(SeasonSeven));
        }
    }

    [Test]
    public async Task SeasonRowLock_OnPostgres_IsSelectForUpdate()
    {
        await using var context = Postgres();

        await new ContextDatabaseLocks(context).LockSeasonRowAsync(7);

        var sent = _connection.Sent.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent.Sql, Does.StartWith("SELECT 1 FROM \"Event\".\"Seasons\" WHERE \"SeasonID\" = @").And.EndWith(" FOR UPDATE"));
            Assert.That(sent.Parameters.Values, Is.EqualTo(SeasonSeven));
        }
    }

    [Test]
    public async Task SeasonRowLock_OnSqlite_IsTheDatabasesOwnWriteLock_SendingNothing()
    {
        await using var context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>().UseSqlite("Data Source=unused.db").Options);

        var locking = new ContextDatabaseLocks(context).LockSeasonRowAsync(7);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(locking.IsCompletedSuccessfully, Is.True, "nothing to wait for: SQLite writes one at a time");
            Assert.That(context.Database.GetDbConnection().State, Is.EqualTo(System.Data.ConnectionState.Closed), "never opened");
        }
        await locking;
    }
}
