using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Infrastructure.DatabaseBackup;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>NativeBackupsTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class NativeBackupsUnitTests
{
    private static readonly string[] ExpectedArgumentList = ["--format=custom", "--no-owner", "--no-privileges", "--file", "/tmp/out.dump", "--dbname", "GrifballWebApp"];

    [Test]
    public void PgDump_gets_the_connection_in_its_environment_and_the_password_never_on_its_command_line()
    {
        var start = NativeBackups.PgDumpStartInfo(
            "Host=db.grif;Port=5433;Database=GrifballWebApp;Username=grif_app;Password=s3cret;SSL Mode=Require;GSS Encryption Mode=Disable",
            "pg_dump", "/tmp/out.dump");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(start.FileName, Is.EqualTo("pg_dump"));
            Assert.That(start.ArgumentList, Is.EqualTo(ExpectedArgumentList));
            Assert.That(start.ArgumentList, Has.None.Contains("s3cret"));
            Assert.That(start.Environment["PGHOST"], Is.EqualTo("db.grif"));
            Assert.That(start.Environment["PGPORT"], Is.EqualTo("5433"));
            Assert.That(start.Environment["PGUSER"], Is.EqualTo("grif_app"));
            Assert.That(start.Environment["PGPASSWORD"], Is.EqualTo("s3cret"));
            Assert.That(start.Environment["PGSSLMODE"], Is.EqualTo("require"));
            Assert.That(start.Environment["PGGSSENCMODE"], Is.EqualTo("disable"));
        }
    }

    [Test]
    public void PgDump_leaves_out_what_the_connection_string_does()
    {
        var start = NativeBackups.PgDumpStartInfo("Port=5432", "pg_dump", "/tmp/out.dump");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(start.ArgumentList[^1], Is.EqualTo(""));
            Assert.That(start.Environment.ContainsKey("PGHOST"), Is.False);
            Assert.That(start.Environment.ContainsKey("PGUSER"), Is.False);
        }
    }

    [TestCase("Disable", "disable")]
    [TestCase("Allow", "allow")]
    [TestCase("Prefer", "prefer")]
    [TestCase("VerifyCA", "verify-ca")]
    [TestCase("VerifyFull", "verify-full")]
    public void PgDump_takes_the_connections_SSL_mode(string mode, string expected)
    {
        var start = NativeBackups.PgDumpStartInfo($"Host=db;Database=d;SSL Mode={mode}", "pg_dump", "/tmp/out.dump");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(start.Environment["PGSSLMODE"], Is.EqualTo(expected));
            Assert.That(start.Environment["PGGSSENCMODE"], Is.EqualTo("prefer"));
            Assert.That(start.Environment.ContainsKey("PGPASSWORD"), Is.False);
        }
    }

    [TestCase(3, "Developer Edition (64-bit)", true)]
    [TestCase(3, "Enterprise Edition: Core-based Licensing (64-bit)", true)]
    [TestCase(2, "Standard Edition (64-bit)", true)]
    [TestCase(2, "Web Edition (64-bit)", false)]
    [TestCase(4, "Express Edition (64-bit)", false)]
    [TestCase(5, "SQL Azure", false)]
    public void SqlServer_compresses_on_the_editions_that_can(int engineEdition, string edition, bool compresses) =>
        Assert.That(NativeBackups.CompressesBackups(engineEdition, edition), Is.EqualTo(compresses));
}
