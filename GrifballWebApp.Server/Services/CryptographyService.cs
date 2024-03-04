using System.Security.Cryptography;
using System.Text;

namespace GrifballWebApp.Server.Services;

public class CryptographyService
{
    const int keySize = 64;
    const int iterations = 350000;
    HashAlgorithmName hashAlgorithm = HashAlgorithmName.SHA512;

    /// <summary>
    /// Hash a password with a random salt
    /// </summary>
    /// <param name="password">The plain text password to be hashed</param>
    /// <param name="salt">The randomly generated salt in base64</param>
    /// <returns>The hashed password in base64</returns>
    public string HashPasword(string password, out string salt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password, nameof(password));
        var saltBytes = RandomNumberGenerator.GetBytes(keySize);
        salt = Convert.ToBase64String(saltBytes);
        if (string.IsNullOrEmpty(salt))
            throw new Exception("Failed to generate random salt");
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltBytes,
            iterations,
            hashAlgorithm,
            keySize);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Hash a password with an existing salt
    /// </summary>
    /// <param name="password">The plain text password to be hashed</param>
    /// <param name="salt">The salt to use in base64</param>
    /// <returns>The hashed password in base64</returns>
    public string HashPasword(string password, string salt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password, nameof(password));
        ArgumentException.ThrowIfNullOrWhiteSpace(salt, nameof(salt));
        var saltBytes = Convert.FromBase64String(salt);
        if (saltBytes is null || saltBytes.Length is 0)
            throw new ArgumentException("Salt is not valid base64", nameof(salt));
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltBytes,
            iterations,
            hashAlgorithm,
            keySize);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Check if a password is correct for a given hash and salt
    /// </summary>
    /// <param name="password">The plain text password to be hashed</param>
    /// <param name="passwordHash">The password hash to use in base64</param>
    /// <param name="salt">The salt to use in base64</param>
    /// <returns>True if correct password</returns>
    public bool IsCorrectPassword(string password, string passwordHash, string salt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password, nameof(password));
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash, nameof(passwordHash));
        ArgumentException.ThrowIfNullOrWhiteSpace(salt, nameof(salt));
        var passwordHashToCheck = HashPasword(password: password, salt: salt);

        return passwordHashToCheck == passwordHash;
    }
}
