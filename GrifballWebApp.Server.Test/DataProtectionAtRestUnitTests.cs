using GrifballWebApp.Server.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using static GrifballWebApp.Server.Test.DataProtectionAtRestTestData;

namespace GrifballWebApp.Server.Test;

/// <summary>DataProtectionAtRestTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class DataProtectionAtRestUnitTests
{
    [Test]
    public void No_certificate_configured_keeps_the_keys_as_they_were()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DataProtectionAtRest.Certificate(Configuration()), Is.Null);
            Assert.That(DataProtectionAtRest.Certificate(Configuration((DataProtectionAtRest.CertificateKey, " "))), Is.Null);
        }
    }
}

/// <summary>DataProtectionAtRestTests' helpers, shared with its integration tests.</summary>
internal static class DataProtectionAtRestTestData
{
    internal static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => KeyValuePair.Create(v.Key, v.Value))).Build();
}
