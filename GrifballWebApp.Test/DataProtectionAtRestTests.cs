using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GrifballWebApp.Server.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Test;

/// <summary>DataProtectionAtRest: the keys encrypted with the configured certificate, and the ones kept in the clear deleted.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class DataProtectionAtRestTests
{
    /// <summary>A folder with a new self-signed certificate (cert.pem), its key (key.pem), and both in one file (both.pem). The caller deletes it.</summary>
    private static string CertificateFolder()
    {
        var folder = Directory.CreateTempSubdirectory("grif-dp-test-").FullName;
        using var rsa = RSA.Create(2048);
        using var certificate = new CertificateRequest("CN=grif data protection test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var cert = certificate.ExportCertificatePem();
        var key = rsa.ExportPkcs8PrivateKeyPem();
        File.WriteAllText(Path.Combine(folder, "cert.pem"), cert);
        File.WriteAllText(Path.Combine(folder, "key.pem"), key);
        File.WriteAllText(Path.Combine(folder, "both.pem"), cert + "\n" + key);
        return folder;
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => KeyValuePair.Create(v.Key, v.Value))).Build();

    /// <summary>A provider keeping its keys in <paramref name="keys"/>, encrypted with <paramref name="certificate"/> if given.</summary>
    private static IDataProtectionProvider Provider(string keys, X509Certificate2? certificate) =>
        new ServiceCollection().AddDataProtection().SetApplicationName("grif-test").PersistKeysToFileSystem(new DirectoryInfo(keys)).ProtectKeysWith(certificate)
            .Services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();

    [Test]
    public void No_certificate_configured_keeps_the_keys_as_they_were()
    {
        Assert.That(DataProtectionAtRest.Certificate(Configuration()), Is.Null);
        Assert.That(DataProtectionAtRest.Certificate(Configuration((DataProtectionAtRest.CertificateKey, " "))), Is.Null);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Keys_made_with_the_certificate_are_encrypted_and_read_back_with_it(bool separateKeyFile)
    {
        var folder = CertificateFolder();
        try
        {
            var configuration = separateKeyFile
                ? Configuration((DataProtectionAtRest.CertificateKey, Path.Combine(folder, "cert.pem")), (DataProtectionAtRest.CertificatePrivateKeyKey, Path.Combine(folder, "key.pem")))
                : Configuration((DataProtectionAtRest.CertificateKey, Path.Combine(folder, "both.pem")));
            using var certificate = DataProtectionAtRest.Certificate(configuration);
            Assert.That(certificate!.HasPrivateKey, Is.True);
            var keys = Directory.CreateDirectory(Path.Combine(folder, "keys")).FullName;

            var protectedText = Provider(keys, certificate).CreateProtector("test").Protect("secret");

            var stored = Directory.GetFiles(keys, "key-*.xml");
            Assert.That(stored, Has.Length.EqualTo(1));
            Assert.That(DataProtectionAtRest.IsUnencryptedKey(File.ReadAllText(stored[0])), Is.False);
            // A new provider (another pod, or after a restart) reads the encrypted key with the certificate.
            Assert.That(Provider(keys, certificate).CreateProtector("test").Unprotect(protectedText), Is.EqualTo("secret"));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public void Tells_keys_in_the_clear_from_encrypted_keys_and_revocations()
    {
        var folder = CertificateFolder();
        try
        {
            var plain = Directory.CreateDirectory(Path.Combine(folder, "plain")).FullName;
            Provider(plain, null).CreateProtector("test").Protect("secret");
            using var certificate = DataProtectionAtRest.Certificate(Configuration((DataProtectionAtRest.CertificateKey, Path.Combine(folder, "both.pem"))));
            var encrypted = Directory.CreateDirectory(Path.Combine(folder, "encrypted")).FullName;
            Provider(encrypted, certificate).CreateProtector("test").Protect("secret");

            Assert.That(DataProtectionAtRest.IsUnencryptedKey(File.ReadAllText(Directory.GetFiles(plain, "key-*.xml").Single())), Is.True);
            Assert.That(DataProtectionAtRest.IsUnencryptedKey(File.ReadAllText(Directory.GetFiles(encrypted, "key-*.xml").Single())), Is.False);
            Assert.That(DataProtectionAtRest.IsUnencryptedKey("<revocation version=\"1\"><revocationDate>2026-01-01T00:00:00Z</revocationDate><key id=\"*\" /></revocation>"), Is.False);
            Assert.That(DataProtectionAtRest.IsUnencryptedKey(null), Is.False);
            Assert.That(DataProtectionAtRest.IsUnencryptedKey(""), Is.False);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task Discards_only_the_keys_kept_in_the_clear()
    {
        var folder = CertificateFolder();
        var context = await SetUpFixture.NewGrifballContext();
        try
        {
            var plain = Directory.CreateDirectory(Path.Combine(folder, "plain")).FullName;
            Provider(plain, null).CreateProtector("test").Protect("secret");
            using var certificate = DataProtectionAtRest.Certificate(Configuration((DataProtectionAtRest.CertificateKey, Path.Combine(folder, "both.pem"))));
            var encrypted = Directory.CreateDirectory(Path.Combine(folder, "encrypted")).FullName;
            Provider(encrypted, certificate).CreateProtector("test").Protect("secret");
            var encryptedXml = File.ReadAllText(Directory.GetFiles(encrypted, "key-*.xml").Single());
            context.DataProtectionKeys.AddRange(
                new DataProtectionKey { FriendlyName = "plain", Xml = File.ReadAllText(Directory.GetFiles(plain, "key-*.xml").Single()) },
                new DataProtectionKey { FriendlyName = "encrypted", Xml = encryptedXml });
            await context.SaveChangesAsync();

            Assert.That(await DataProtectionAtRest.DiscardUnencryptedKeysAsync(context), Is.EqualTo(1));
            Assert.That(await DataProtectionAtRest.DiscardUnencryptedKeysAsync(context), Is.EqualTo(0));

            var left = await context.DataProtectionKeys.AsNoTracking().ToListAsync();
            Assert.That(left.Select(k => k.FriendlyName), Is.EqualTo(new[] { "encrypted" }));
        }
        finally
        {
            Directory.Delete(folder, true);
            await context.DropDatabaseAndDispose();
        }
    }
}
