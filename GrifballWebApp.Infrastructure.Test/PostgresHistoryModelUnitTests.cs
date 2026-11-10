using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>PostgresHistoryModelTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
public class PostgresHistoryModelUnitTests
{
    [Test]
    public void HistoryFunctionsVersion_HasUnixLineEndings()
    {
        var version = new HistoryFunctionsVersion(9, "a\r\nb", "c\r\nd");

        Assert.That((version.Create, version.Drop), Is.EqualTo(("a\nb", "c\nd")));
    }
}
