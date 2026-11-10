using GrifballWebApp.Infrastructure.Conversion;
using GrifballWebApp.Server.DatabaseBackup;
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
}
