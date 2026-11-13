using GrifballWebApp.Server.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void IsUnencryptedKey_IsFalse_ForBlankXml(string? xml)
    {
        Assert.That(DataProtectionAtRest.IsUnencryptedKey(xml), Is.False);
    }

    [Test]
    public void IsUnencryptedKey_IsFalse_ForMalformedXml()
    {
        Assert.That(DataProtectionAtRest.IsUnencryptedKey("<key"), Is.False);
    }

    [Test]
    public void IsUnencryptedKey_IsFalse_ForANonKeyElement()
    {
        Assert.That(DataProtectionAtRest.IsUnencryptedKey("<revocation />"), Is.False);
    }

    [Test]
    public void IsUnencryptedKey_IsFalse_ForAKeyWithAnEncryptedSecret()
    {
        Assert.That(DataProtectionAtRest.IsUnencryptedKey("<key id=\"1\"><encryption><encryptedSecret>abc</encryptedSecret></encryption></key>"), Is.False);
    }

    [Test]
    public void IsUnencryptedKey_IsTrue_ForAKeyWithNoEncryptedSecret()
    {
        Assert.That(DataProtectionAtRest.IsUnencryptedKey("<key id=\"1\"><descriptor>a</descriptor></key>"), Is.True);
    }
}

/// <summary>DataProtectionAtRest.DiscardUnencryptedKeysAsync's unit tests, on a real GrifballContext (temp-file SQLite, #402).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscardUnencryptedKeysUnitTests : SqliteGrifballContextFixture
{
    [Test]
    public async Task DiscardUnencryptedKeysAsync_ReturnsZero_WhenThereAreNoKeys()
    {
        Assert.That(await DataProtectionAtRest.DiscardUnencryptedKeysAsync(Db, CancellationToken.None), Is.Zero);
    }

    [Test]
    public async Task DiscardUnencryptedKeysAsync_RemovesOnlyTheUnencryptedKeys()
    {
        Db.DataProtectionKeys.Add(new DataProtectionKey { FriendlyName = "clear", Xml = "<key id=\"1\"><descriptor>a</descriptor></key>" });
        Db.DataProtectionKeys.Add(new DataProtectionKey { FriendlyName = "sealed", Xml = "<key id=\"2\"><encryption><encryptedSecret>x</encryptedSecret></encryption></key>" });
        await Db.SaveChangesAsync();

        var discarded = await DataProtectionAtRest.DiscardUnencryptedKeysAsync(Db, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(discarded, Is.EqualTo(1));
            Assert.That(await Db.DataProtectionKeys.Select(k => k.FriendlyName).ToListAsync(), Is.EquivalentTo(new[] { "sealed" }));
        }
    }

    [Test]
    public async Task DiscardUnencryptedKeysAsync_WithACertificate_DoesNothing_WhenNoneIsGiven()
    {
        // certificate is null: the overload returns before ever touching context, so null! is safe here
        await DataProtectionAtRest.DiscardUnencryptedKeysAsync(null!, certificate: null, new RecordingLogger<DiscardUnencryptedKeysUnitTests>(), CancellationToken.None);

        Assert.Pass("No exception: the context was never reached");
    }

    [Test]
    public async Task DiscardUnencryptedKeysAsync_WithACertificate_LogsAndDiscards_WhenThereAreUnencryptedKeys()
    {
        Db.DataProtectionKeys.Add(new DataProtectionKey { FriendlyName = "clear", Xml = "<key id=\"1\"><descriptor>a</descriptor></key>" });
        await Db.SaveChangesAsync();
        var logger = new RecordingLogger<DiscardUnencryptedKeysUnitTests>();
        using var certificate = TestCertificate.SelfSigned();

        await DataProtectionAtRest.DiscardUnencryptedKeysAsync(Db, certificate, logger, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Db.DataProtectionKeys.CountAsync(), Is.Zero);
            var entry = logger.Entries.Single();
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(entry.Message, Does.Contain("Deleted 1 data protection key"));
        }
    }

    [Test]
    public async Task DiscardUnencryptedKeysAsync_WithACertificate_LogsNothing_WhenThereAreNoUnencryptedKeys()
    {
        var logger = new RecordingLogger<DiscardUnencryptedKeysUnitTests>();
        using var certificate = TestCertificate.SelfSigned();

        await DataProtectionAtRest.DiscardUnencryptedKeysAsync(Db, certificate, logger, CancellationToken.None);

        Assert.That(logger.Entries, Is.Empty);
    }
}

/// <summary>A throwaway self-signed certificate, for tests that only need a non-null one.</summary>
internal static class TestCertificate
{
    internal static System.Security.Cryptography.X509Certificates.X509Certificate2 SelfSigned()
    {
        using var key = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=grif-server-test", key, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.X509Certificates.RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}

/// <summary>DataProtectionAtRestTests' helpers, shared with its integration tests.</summary>
internal static class DataProtectionAtRestTestData
{
    internal static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => KeyValuePair.Create(v.Key, v.Value))).Build();
}
