using System.Security.Cryptography;
using System.Text;

namespace GrifballWebApp.Server.Services;

public class CryptographyService
{
    const int keySize = 64;
    const int iterations = 350000;
    HashAlgorithmName hashAlgorithm = HashAlgorithmName.SHA512;

    public string HashPasword(string password, out byte[] salt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password, nameof(password));
        salt = RandomNumberGenerator.GetBytes(keySize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            hashAlgorithm,
            keySize);
        return Convert.ToHexString(hash);
    }
}
