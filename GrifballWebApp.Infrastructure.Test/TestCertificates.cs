using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>Certificates made for a test (ScratchScalerTests, ScratchScalerInClusterTests).</summary>
internal static class TestCertificates
{
    /// <summary>A self-signed certificate authority named <paramref name="name"/>, with its key.</summary>
    internal static X509Certificate2 Ca(string name, out RSA key)
    {
        key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
