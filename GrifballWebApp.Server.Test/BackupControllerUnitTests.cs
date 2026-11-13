using GrifballWebApp.Infrastructure.Conversion;
using GrifballWebApp.Server.DatabaseBackup;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using GrifballWebApp.Infrastructure.DatabaseBackup;

namespace GrifballWebApp.Server.Test;

/// <summary>BackupControllerTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class BackupControllerUnitTests
{
    [Test]
    public void Describe_points_at_the_network_policy_when_a_server_cant_be_reached()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DatabaseBackupService.Describe(new ConversionException("Refused.")), Is.EqualTo("Refused."));
            Assert.That(DatabaseBackupService.Describe(new TimeoutException("Timed out.")), Does.StartWith("Timed out.").And.Contain("networkPolicy.backend.extraEgress"));
            Assert.That(DatabaseBackupService.Describe(new SqliteException("Unable to open.", 14)), Does.Contain("networkPolicy.backend.extraEgress"));
            Assert.That(DatabaseBackupService.Describe(new InvalidOperationException("Other.")), Is.EqualTo("Other."));
        }
    }

    [Test]
    public async Task Without_a_connection_string_it_says_so()
    {
        var service = new DatabaseBackupService(new ConfigurationBuilder().Build(), NullLoggerFactory.Instance, TimeProvider.System);

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(BackupFormat.Sqlite, CancellationToken.None));
        Assert.That(e!.Message, Does.Contain("ConnectionStrings:GrifballWebApp"));
    }

    [Test]
    public void Is_sysadmin_only()
    {
        Assert.That(ControllerTestHelpers.ClassAuthorize(typeof(BackupController))?.Roles, Is.EqualTo("Sysadmin"));
    }

    // Refused before BackupRuns/BackupDownloads are ever asked
    private static BackupController Controller() => new(null!, null!, null!, null!, new ConfigurationBuilder().Build(), NullLogger<BackupController>.Instance);

    [Test]
    public async Task StartDownload_RepliesBadRequest_ForAnUnknownFormat()
    {
        var result = await Controller().StartDownload("not-a-format", CancellationToken.None);

        result.AssertProblem(StatusCodes.Status400BadRequest, "The format must be sqlite, postgres, sqlserver, bak or dump.");
    }

    [Test]
    public async Task DownloadStatus_RepliesBadRequest_ForAnUnknownFormat()
    {
        var result = await Controller().DownloadStatus("not-a-format", CancellationToken.None);

        result.Result.AssertProblem(StatusCodes.Status400BadRequest, "The format must be sqlite, postgres, sqlserver, bak or dump.");
    }

    [Test]
    public async Task Download_RepliesBadRequest_ForAnUnknownFormat()
    {
        var result = await Controller().Download("not-a-format", CancellationToken.None);

        result.AssertProblem(StatusCodes.Status400BadRequest, "The format must be sqlite, postgres, sqlserver, bak or dump.");
    }

    [Test]
    public async Task Export_RepliesBadRequest_ForAnUnknownProvider()
    {
        var result = await Controller().Export(new ExportRequestDto("not-a-provider", "cs"), CancellationToken.None);

        result.AssertProblem(StatusCodes.Status400BadRequest, "The provider must be SqlServer, Postgres or Sqlite.");
    }
}

/// <summary>BackupController.Info on a real GrifballContext (temp-file SQLite, #402): Database.GetMigrations() is
/// the assembly's own, so it works with no server, and DatabaseBackupService's Formats needs only configuration.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class BackupControllerInfoUnitTests : SqliteGrifballContextFixture
{
    [Test]
    public void Info_ReturnsTheProviderAndTheFormatsAvailableHere()
    {
        var backups = new DatabaseBackupService(new ConfigurationBuilder().Build(), NullLoggerFactory.Instance, TimeProvider.System);
        var controller = new BackupController(backups, null!, null!, Db, new ConfigurationBuilder().Build(), NullLogger<BackupController>.Instance);

        var info = controller.Info();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(info.LatestMigration, Is.Not.Null.And.Not.Empty);
            // Database:Provider defaults to SqlServer unset, so HasNativeServer(SqlServer) is true here: bak is offered too
            Assert.That(info.Formats, Is.EquivalentTo(new[] { "sqlite", "postgres", "sqlserver", "bak" }));
        }
    }
}
