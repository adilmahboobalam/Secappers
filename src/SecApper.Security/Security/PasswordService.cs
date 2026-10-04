using System;
using System.Security.Cryptography;
using System.Text;

namespace SecApper.Security.Security;

public class PasswordService : IPasswordService
{
    public const string DefaultAlgorithm = "PBKDF2-SHA256";
    public const int DefaultIterations = 310000;
    public const int SaltLengthBytes = 32;
    public const int HashLengthBytes = 32;
    public const int MinimumPasswordLength = 6;

    public PasswordHashResult HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be empty.", nameof(password));

        byte[] salt = RandomNumberGenerator.GetBytes(SaltLengthBytes);
        byte[] hash = DeriveKey(password, salt, DefaultIterations, HashLengthBytes);

        return new PasswordHashResult(hash, salt, DefaultAlgorithm, DefaultIterations);
    }

    public bool VerifyPassword(string password, byte[] storedHash, byte[] salt, string algorithm, int iterations)
    {
        if (string.IsNullOrEmpty(password) || storedHash == null || salt == null)
            return false;

        if (storedHash.Length == 0 || salt.Length == 0)
            return false;

        if (algorithm != DefaultAlgorithm)
        {
            // Unsupported algorithm
            return false;
        }

        byte[] computedHash = DeriveKey(password, salt, iterations, storedHash.Length);

        try
        {
            return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(computedHash);
        }
    }

    public (bool IsValid, string? ErrorMessage) ValidatePasswordStrength(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "Password cannot be empty or contain only whitespace.");

        if (password.Length < MinimumPasswordLength)
            return (false, $"Password must be at least {MinimumPasswordLength} characters long.");

        return (true, null);
    }

    private static byte[] DeriveKey(string password, byte[] salt, int iterations, int outputLength)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                passwordBytes,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                outputLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }
}
