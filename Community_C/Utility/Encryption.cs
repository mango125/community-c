using Community_C.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text;

namespace Community_C.Utility;

public static class Encryption
{
    public static string HashPassword(
        IPasswordHasher<User> passwordHasher,
        User user,
        string password)
    {
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrEmpty(password);

        return passwordHasher.HashPassword(user, password);
    }

    public static PasswordVerificationResult VerifyPassword(
        IPasswordHasher<User> passwordHasher,
        User user,
        string passwordHash,
        string providedPassword)
    {
        ArgumentNullException.ThrowIfNull(passwordHasher);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrEmpty(passwordHash);
        ArgumentException.ThrowIfNullOrEmpty(providedPassword);

        return passwordHasher.VerifyHashedPassword(
            user,
            passwordHash,
            providedPassword);
    }

    public static string? HashRefreshToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
        byte[] hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToBase64String(hashBytes);
    }

    public static string CreateRandomUrlSafeToken(int byteLength = 32)
    {
        if (byteLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength));
        }

        return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(byteLength));
    }

    public static bool FixedTimeEquals(string? expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        byte[] actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
