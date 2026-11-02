using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using GrifballWebApp.Database;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Identity;

/// <summary>
/// The data protection keys (sign-in cookies, reset and download tokens, the export's connection string) are kept in
/// the database (Auth.DataProtectionKeys), so every backup has them. With a certificate (DataProtection:Certificate,
/// a PEM file, and DataProtection:CertificateKey, its PEM private key unless the certificate file has it), each new
/// key is encrypted with it, and a backup alone can't sign anyone in. Keys made before, still in the clear, are
/// deleted at startup (<see cref="DiscardUnencryptedKeysAsync"/>): an expired key still reads what it protected, so
/// one in a backup could forge a sign-in cookie for as long as the app kept it. Everyone signs in again once.
/// The certificate has to stay: a new one can't read the keys the old one encrypted (everyone signs in again).
/// </summary>
public static class DataProtectionAtRest
{
    public const string CertificateKey = "DataProtection:Certificate";
    public const string CertificatePrivateKeyKey = "DataProtection:CertificateKey";

    /// <summary>The certificate configured to encrypt the keys; null when none is (they're kept in the clear).</summary>
    public static X509Certificate2? Certificate(IConfiguration configuration)
    {
        var certificate = configuration[CertificateKey];
        if (string.IsNullOrWhiteSpace(certificate))
            return null;
        var key = configuration[CertificatePrivateKeyKey];
        using var pem = string.IsNullOrWhiteSpace(key)
            ? X509Certificate2.CreateFromPemFile(certificate)
            : X509Certificate2.CreateFromPemFile(certificate, key);
        // Through PKCS#12: a PEM key is ephemeral, which XML decryption can't use on Windows.
        return X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pkcs12), null);
    }

    /// <summary>New keys encrypted with the certificate, and the keys it encrypted read with it; unchanged without one.</summary>
    public static IDataProtectionBuilder ProtectKeysWith(this IDataProtectionBuilder builder, X509Certificate2? certificate) =>
        certificate is null ? builder : builder.ProtectKeysWithCertificate(certificate).UnprotectKeysWithAnyCertificate(certificate);

    /// <summary>Whether the stored element is a key in the clear: a key element without an encryptedSecret (revocations aren't keys).</summary>
    public static bool IsUnencryptedKey(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return false;
        var element = XElement.Parse(xml);
        return element.Name.LocalName == "key" && !element.Descendants().Any(e => e.Name.LocalName == "encryptedSecret");
    }

    /// <summary>Deletes the keys kept in the clear; returns how many. Run once a certificate encrypts the new ones.</summary>
    public static async Task<int> DiscardUnencryptedKeysAsync(GrifballContext context, CancellationToken cancellationToken = default)
    {
        var unencrypted = (await context.DataProtectionKeys.ToListAsync(cancellationToken)).Where(k => IsUnencryptedKey(k.Xml)).ToList();
        if (unencrypted.Count == 0)
            return 0;
        context.DataProtectionKeys.RemoveRange(unencrypted);
        await context.SaveChangesAsync(cancellationToken);
        return unencrypted.Count;
    }
}
