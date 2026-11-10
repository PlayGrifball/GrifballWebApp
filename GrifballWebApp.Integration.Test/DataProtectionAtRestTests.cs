using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GrifballWebApp.Server.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static GrifballWebApp.Server.Test.DataProtectionAtRestTestData;

namespace GrifballWebApp.Integration.Test;

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

    /// <summary>A provider keeping its keys in <paramref name="keys"/>, encrypted with <paramref name="certificate"/> if given.</summary>
    private static IDataProtectionProvider Provider(string keys, X509Certificate2? certificate) =>
        new ServiceCollection().AddDataProtection().SetApplicationName("grif-test").PersistKeysToFileSystem(new DirectoryInfo(keys)).ProtectKeysWith(certificate)
            .Services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();

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
            Assert.That(DataProtectionAtRest.IsUnencryptedKey("<key id=\"broken\""), Is.False, "Unreadable XML is left alone");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// The certificate gone (<paramref name="toOtherCertificate"/> false) or replaced by another: the keys it encrypted
    /// can't be read, so they're passed over and a new key is made (in the clear, or with the new certificate). The app
    /// keeps working; only what the old keys protected (sign-in cookies) is lost: everyone signs in again.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void Keys_the_certificate_cant_read_are_passed_over_and_a_new_key_made(bool toOtherCertificate)
    {
        var folder = CertificateFolder();
        var other = CertificateFolder();
        try
        {
            using var certificate = DataProtectionAtRest.Certificate(Configuration((DataProtectionAtRest.CertificateKey, Path.Combine(folder, "both.pem"))));
            var keys = Directory.CreateDirectory(Path.Combine(folder, "keys")).FullName;
            var signedIn = Provider(keys, certificate).CreateProtector("test").Protect("cookie");

            using var otherCertificate = toOtherCertificate
                ? DataProtectionAtRest.Certificate(Configuration((DataProtectionAtRest.CertificateKey, Path.Combine(other, "both.pem"))))
                : null;
            var after = Provider(keys, otherCertificate).CreateProtector("test");

            Assert.That(after.Unprotect(after.Protect("secret")), Is.EqualTo("secret"));
            Assert.Throws<System.Security.Cryptography.CryptographicException>(() => after.Unprotect(signedIn));
            var stored = Directory.GetFiles(keys, "key-*.xml").Select(File.ReadAllText).ToList();
            Assert.That(stored, Has.Count.EqualTo(2));
            // The new key: encrypted with the new certificate, or in the clear without one.
            Assert.That(stored.Count(DataProtectionAtRest.IsUnencryptedKey), Is.EqualTo(toOtherCertificate ? 0 : 1));
        }
        finally
        {
            Directory.Delete(folder, true);
            Directory.Delete(other, true);
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

            // Without a certificate (startup): nothing deleted.
            await DataProtectionAtRest.DiscardUnencryptedKeysAsync(context, null, NullLogger.Instance);
            Assert.That(await context.DataProtectionKeys.CountAsync(), Is.EqualTo(2));

            // With one (startup): the key in the clear deleted.
            await DataProtectionAtRest.DiscardUnencryptedKeysAsync(context, certificate, NullLogger.Instance);
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
