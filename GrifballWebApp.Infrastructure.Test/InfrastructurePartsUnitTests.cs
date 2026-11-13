using GrifballWebApp.Infrastructure.Conversion;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using GrifballWebApp.Infrastructure.Discord;
using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
#pragma warning disable EF1001 // SQL Server's temporal annotation names, as the generator under test reads them
using Microsoft.EntityFrameworkCore.SqlServer.Metadata.Internal;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using Quartz.Extensibility;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// Smaller pieces with no database server: the design-time context (dotnet ef's) on each provider, from the command
/// line's connection string; the Discord gateway's health; the scratch server's stop queued at startup (retried while the
/// job store doesn't answer); SQL Server's migrations SQL around a temporal table's period columns.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class InfrastructurePartsUnitTests
{
    // ---- DesignTimeContext ----

    [TestCase(DatabaseProvider.Sqlite, "Data Source=design.db", "Microsoft.EntityFrameworkCore.Sqlite", "design.db")]
    [TestCase(DatabaseProvider.Postgres, "Host=db;Database=grifdesign;Username=u;Password=p", "Npgsql.EntityFrameworkCore.PostgreSQL", "grifdesign")]
    [TestCase(DatabaseProvider.SqlServer, "Server=db;Database=grifdesign;User Id=u;Password=p", "Microsoft.EntityFrameworkCore.SqlServer", "grifdesign")]
    public async Task DesignTime_UsesTheCommandLinesConnectionString_OnTheProvider(DatabaseProvider provider, string connectionString, string providerName, string database)
    {
        await using var context = DesignTimeContext.Create([$"--ConnectionStrings:GrifballWebApp={connectionString}"], provider);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.Database.ProviderName, Is.EqualTo(providerName));
            Assert.That(context.Database.GetConnectionString(), Does.Contain(database));
        }
    }

    // ---- DiscordGatewayHealthCheck ----

    private static HealthCheckContext Context(IHealthCheck check) => new()
    {
        Registration = new HealthCheckRegistration("discord", check, HealthStatus.Unhealthy, null),
    };

    [Test]
    public async Task DiscordGateway_Connected_IsHealthy_AndADisconnectFailsIt()
    {
        var check = new DiscordGatewayHealthCheck(connected: true);

        var connected = await check.CheckHealthAsync(Context(check));
        await check.SetConnected(false);
        var disconnected = await check.CheckHealthAsync(Context(check));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(connected.Status, Is.EqualTo(HealthStatus.Healthy));
            Assert.That((disconnected.Status, disconnected.Description), Is.EqualTo((HealthStatus.Unhealthy, (string?)"Discord gateway is not connected")));
        }
    }

    [Test]
    public async Task DiscordGateway_AReconnect_IsHealthyAgain_AndHostingItDoesNothingElse()
    {
        var check = new DiscordGatewayHealthCheck(connected: false);

        await check.StartAsync(CancellationToken.None);
        await check.SetConnected(true);
        await check.StopAsync(CancellationToken.None);

        Assert.That((await check.CheckHealthAsync(Context(check))).Status, Is.EqualTo(HealthStatus.Healthy));
    }

    // ---- ScratchScaleDown ----

    private readonly ISchedulerFactory _schedulers = Substitute.For<ISchedulerFactory>();
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
    private readonly IExecutionHistoryStore _history = Substitute.For<IExecutionHistoryStore>();
    private static readonly TriggerKey StopKey = OnDemandJobs.TriggerKeyOf<ScratchStopJob>(ScratchStopJob.RunName);

    private void SchedulerAnswers()
    {
        _scheduler.SchedulerName.Returns("test-scheduler");
        _scheduler.TimeProvider.Returns(new FakeTimeProvider(new DateTimeOffset(2026, 11, 13, 0, 0, 0, TimeSpan.Zero)));
        _scheduler.GetTriggerState(Arg.Any<TriggerKey>(), Arg.Any<CancellationToken>()).Returns(TriggerState.None);
        _scheduler.QueryFireInstances(Arg.Any<FireInstanceQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<FireInstance>([], false));
        _history.QueryExecutions(Arg.Any<ExecutionHistoryQuery>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<ExecutionHistoryEntry>([], false));
    }

    private async Task RunScaleDownAsync()
    {
        using var scaleDown = new ScratchScaleDown(new OnDemandJobs(_schedulers, _history), NullLogger<ScratchScaleDown>.Instance) { RetryDelay = TimeSpan.FromMilliseconds(1) };
        await scaleDown.StartAsync(CancellationToken.None);
        await scaleDown.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(60));
        await scaleDown.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task ScaleDown_QueuesTheScratchServersStop_InTheBackupsGroup_ThenEnds()
    {
        SchedulerAnswers();
        _schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);

        await RunScaleDownAsync();

        await _scheduler.Received(1).ScheduleTrigger(
            Arg.Is<ITrigger>(t => t.Key.Equals(StopKey) && t.ExecutionGroup == BackupRuns.ExecutionGroup
                && new ScratchStopRequest(Environment.MachineName).Equals(t.JobDataMap[SchedulerConstants.JobInput])),
            TriggerConflict.Keep, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ScaleDown_TheJobStoreNotAnswering_IsTriedAgain()
    {
        SchedulerAnswers();
        _schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(
            _ => throw new SchedulerException("store down"),
            _ => ValueTask.FromResult(_scheduler));

        await RunScaleDownAsync();

        await _schedulers.Received().GetScheduler(Arg.Any<CancellationToken>());
        await _scheduler.Received(1).ScheduleTrigger(Arg.Is<ITrigger>(t => t.Key.Equals(StopKey)), TriggerConflict.Keep, Arg.Any<CancellationToken>());
    }

    // ---- SqlServerTemporalMigrationsSqlGenerator ----

    private static AddColumnOperation Column(string name, string? periodAnnotation = null)
    {
        var column = new AddColumnOperation { Schema = "dbo", Table = "Teams", Name = name, ClrType = typeof(int), ColumnType = "int", IsNullable = true };
        if (periodAnnotation is not null)
            column[periodAnnotation] = true;
        return column;
    }

    [Test]
    public void IsPeriodColumn_EitherEndOfThePeriod()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SqlServerTemporalMigrationsSqlGenerator.IsPeriodColumn(Column("PeriodStart", SqlServerAnnotationNames.TemporalIsPeriodStartColumn)), Is.True);
            Assert.That(SqlServerTemporalMigrationsSqlGenerator.IsPeriodColumn(Column("PeriodEnd", SqlServerAnnotationNames.TemporalIsPeriodEndColumn)), Is.True);
            Assert.That(SqlServerTemporalMigrationsSqlGenerator.IsPeriodColumn(Column("Score")), Is.False);
        }
    }

    [Test]
    public async Task SqlServerMigrations_APlainColumn_IsAddedAsEver_WithNoDefaultDropped()
    {
        await using var context = DatabaseConverter.NewContext(new DatabaseEndpoint(DatabaseProvider.SqlServer, "Server=localhost;Database=GrifballWebApp"));
        var generator = context.GetService<IMigrationsSqlGenerator>();

        var sql = string.Join("\n", generator.Generate([Column("Score")]).Select(c => c.CommandText));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(generator, Is.TypeOf<SqlServerTemporalMigrationsSqlGenerator>());
            Assert.That(sql, Does.Contain("ALTER TABLE [dbo].[Teams] ADD [Score] int NULL;"));
            Assert.That(sql, Does.Not.Contain("DROP CONSTRAINT"));
        }
    }

    // ---- SqlDialect ----

    [Test]
    public void SqlDialect_ForAnUnknownProvider_IsRefused()
        => Assert.That(() => SqlDialect.For((DatabaseProvider)99), Throws.InstanceOf<ArgumentOutOfRangeException>());
}
