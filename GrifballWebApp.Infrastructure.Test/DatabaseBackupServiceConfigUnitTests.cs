using System.Data.Common;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Conversion;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>A stand-in for a provider's own DbException (SqlException and friends have no public constructor).</summary>
internal sealed class FakeDbException(string message) : DbException(message)
{
}

/// <summary>
/// DatabaseBackupService's configuration-only surface: which formats are offered, where a native backup can be made,
/// and the messages shown for a bad connection string or a failed backup or export. No database, process or network:
/// BuildAsync (DatabaseConverter, NativeBackups) stays GrifballWebApp.Integration.Test's.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class DatabaseBackupServiceConfigUnitTests
{
    private static readonly BackupFormat[] expectedWithNeither = [BackupFormat.Sqlite, BackupFormat.Postgres, BackupFormat.SqlServer];
    private static readonly BackupFormat[] expectedWithBak = [.. expectedWithNeither, BackupFormat.Bak];
    private static readonly BackupFormat[] expectedWithDump = [.. expectedWithNeither, BackupFormat.Dump];

    private static DatabaseBackupService Service(Dictionary<string, string?> config) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(config).Build(), NullLoggerFactory.Instance, new FakeTimeProvider());

    private static Dictionary<string, string?> Config(string ownProvider, string? scratchProvider = null, string? scratchConnectionString = null) => new()
    {
        ["Database:Provider"] = ownProvider,
        ["ConnectionStrings:GrifballWebApp"] = "Data Source=own.db",
        ["DatabaseBackup:Scratch:Provider"] = scratchProvider,
        ["DatabaseBackup:Scratch:ConnectionString"] = scratchConnectionString,
    };

    [Test]
    public void Formats_WithNoNativeServerEitherSide_OffersOnlyTheThreeScripts()
        => Assert.That(Service(Config("Sqlite")).Formats, Is.EqualTo(expectedWithNeither));

    [Test]
    public void Formats_OwnServerIsSqlServer_AddsBak()
        => Assert.That(Service(Config("SqlServer")).Formats, Is.EqualTo(expectedWithBak));

    [Test]
    public void Formats_OwnServerIsPostgres_AddsDump()
        => Assert.That(Service(Config("Postgres")).Formats, Is.EqualTo(expectedWithDump));

    [Test]
    public void Formats_ScratchServerOffersWhatTheOwnServerDoesnt()
        => Assert.That(Service(Config("Sqlite", "Postgres", "Host=scratch;Database=d")).Formats, Is.EqualTo(expectedWithDump));

    [Test]
    public void Formats_ScratchProviderAloneWithNoConnectionString_IsNotANativeServer()
        // An API pod: DatabaseBackup:Scratch:Provider without the connection string offers the format, but can't make it itself
        => Assert.That(Service(Config("Sqlite", "SqlServer")).Formats, Is.EqualTo(expectedWithBak));

    [Test]
    public void RequireNativeServer_ThrowsWhenNeitherSideIsOfThatKind()
    {
        var service = Service(Config("Sqlite"));

        using (Assert.EnterMultipleScope())
        {
            var bak = Assert.Throws<NativeBackupUnavailableException>(() => service.RequireNativeServer(BackupFormat.Bak));
            Assert.That(bak!.Message, Does.Contain(".bak").And.Contain("SQL Server").And.Contain("backupScratch"));
            var dump = Assert.Throws<NativeBackupUnavailableException>(() => service.RequireNativeServer(BackupFormat.Dump));
            Assert.That(dump!.Message, Does.Contain(".dump").And.Contain("PostgreSQL").And.Contain("backupScratch"));
        }
    }

    [Test]
    public void RequireNativeServer_DoesNotThrow_WhenTheOwnServerIsOfThatKind()
    {
        var service = Service(Config("SqlServer"));
        Assert.That(() => service.RequireNativeServer(BackupFormat.Bak), Throws.Nothing);
    }

    [Test]
    public void RequireNativeSource_OwnServer_ReturnsItWithoutScratch()
    {
        var service = Service(Config("SqlServer"));

        var source = service.RequireNativeSource(BackupFormat.Bak, out var scratch);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(scratch, Is.False);
            Assert.That(source, Is.EqualTo(new DatabaseEndpoint(DatabaseProvider.SqlServer, "Data Source=own.db")));
        }
    }

    [Test]
    public void RequireNativeSource_NeitherSideMatches_ThrowsUnavailable()
    {
        var service = Service(Config("Sqlite"));
        Assert.Throws<NativeBackupUnavailableException>(() => service.RequireNativeSource(BackupFormat.Dump, out _));
    }

    [Test]
    public void RequireNativeSource_FallsBackToTheScratchServer_WhenTheOwnServerIsntOfThatKind()
    {
        var service = Service(Config("Sqlite", "Postgres", "Host=scratch;Database=d"));

        var source = service.RequireNativeSource(BackupFormat.Dump, out var scratch);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(scratch, Is.True);
            Assert.That(source, Is.EqualTo(new DatabaseEndpoint(DatabaseProvider.Postgres, "Host=scratch;Database=d")));
        }
    }

    [Test]
    public void RequireNativeSource_ScratchProviderWithoutConnectionString_Throws()
    {
        var service = Service(Config("Sqlite", "Postgres"));
        var e = Assert.Throws<NativeBackupUnavailableException>(() => service.RequireNativeSource(BackupFormat.Dump, out _));
        Assert.That(e!.Message, Does.Contain("backupScratch"));
    }

    [Test]
    public void Validate_RejectsAnEmptyConnectionString()
        => Assert.That(() => DatabaseBackupService.Validate(DatabaseProvider.Postgres, ""), Throws.ArgumentException);

    [Test]
    public void Validate_RejectsAKeyTheProviderDoesntKnow()
        => Assert.That(() => DatabaseBackupService.Validate(DatabaseProvider.SqlServer, "NotAKey=1"), Throws.ArgumentException);

    [TestCase(DatabaseProvider.SqlServer, "Server=.;Database=d;")]
    [TestCase(DatabaseProvider.Postgres, "Host=db;Database=d")]
    [TestCase(DatabaseProvider.Sqlite, "Data Source=file.db")]
    public void Validate_AcceptsAWellFormedConnectionStringForEachProvider(DatabaseProvider provider, string connectionString)
        => Assert.That(() => DatabaseBackupService.Validate(provider, connectionString), Throws.Nothing);

    [Test]
    public void DescribeBackup_OperationCancelled_SaysTheAppStopped()
        => Assert.That(DatabaseBackupService.DescribeBackup(new OperationCanceledException()), Is.EqualTo("The app stopped while the backup was being made."));

    [Test]
    public void DescribeBackup_OtherException_IsItsMessage()
        => Assert.That(DatabaseBackupService.DescribeBackup(new InvalidOperationException("boom")), Is.EqualTo("boom"));

    [Test]
    public void Describe_ConversionException_IsItsMessageAsIs()
        => Assert.That(DatabaseBackupService.Describe(new ConversionException("a table didn't match")), Is.EqualTo("a table didn't match"));

    [Test]
    public void Describe_OperationCancelled_SaysTheTargetWasPutBack()
        => Assert.That(DatabaseBackupService.Describe(new OperationCanceledException()), Is.EqualTo("The app stopped during the export; the target was put back as it was."));

    [Test]
    public void Describe_DbException_MentionsTheNetworkPolicy()
    {
        var e = new FakeDbException("could not connect");

        Assert.That(DatabaseBackupService.Describe(e), Does.Contain("could not connect").And.Contain("networkPolicy.backend.extraEgress"));
    }

    [Test]
    public void Describe_TimeoutException_MentionsTheNetworkPolicy()
        => Assert.That(DatabaseBackupService.Describe(new TimeoutException("too slow")), Does.Contain("too slow").And.Contain("networkPolicy.backend.extraEgress"));

    [Test]
    public void Describe_OtherException_IsItsMessage()
        => Assert.That(DatabaseBackupService.Describe(new InvalidOperationException("boom")), Is.EqualTo("boom"));
}
