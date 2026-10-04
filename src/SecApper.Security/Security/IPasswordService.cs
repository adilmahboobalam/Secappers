namespace SecApper.Security.Security;

public record PasswordHashResult(byte[] Hash, byte[] Salt, string Algorithm, int Iterations);

public interface IPasswordService
{
    PasswordHashResult HashPassword(string password);
    bool VerifyPassword(string password, byte[] storedHash, byte[] salt, string algorithm, int iterations);
    (bool IsValid, string? ErrorMessage) ValidatePasswordStrength(string password);
}
