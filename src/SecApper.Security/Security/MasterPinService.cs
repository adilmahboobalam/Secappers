using System;
using System.Threading.Tasks;
using SecApper.Security.Data;

namespace SecApper.Security.Security;

public class MasterPinService : IMasterPinService
{
    private readonly IDatabaseService _db;
    private readonly IPasswordService _passwordService;

    public const string KeyConfigured = "MasterPinConfigured";
    public const string KeyHash = "MasterPinHash";
    public const string KeySalt = "MasterPinSalt";
    public const string KeyAlgorithm = "MasterPinAlgorithm";
    public const string KeyIterations = "MasterPinIterations";

    public MasterPinService(IDatabaseService db, IPasswordService passwordService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
    }

    public async Task<bool> IsMasterPinConfiguredAsync()
    {
        string? configured = await _db.GetSettingAsync(KeyConfigured);
        if (string.Equals(configured, "true", StringComparison.OrdinalIgnoreCase))
        {
            string? hash = await _db.GetSettingAsync(KeyHash);
            return !string.IsNullOrWhiteSpace(hash);
        }
        return false;
    }

    public (bool IsValid, string? ErrorMessage) ValidatePin(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
            return (false, "Master PIN cannot be empty.");

        pin = pin.Trim();
        if (pin.Length < 4)
            return (false, "Master PIN must be at least 4 digits or characters.");

        if (pin.Length > 32)
            return (false, "Master PIN must be 32 characters or fewer.");

        return (true, null);
    }

    public async Task<bool> SetMasterPinAsync(string pin)
    {
        var validation = ValidatePin(pin);
        if (!validation.IsValid)
            throw new ArgumentException(validation.ErrorMessage ?? "Invalid Master PIN", nameof(pin));

        var hashResult = _passwordService.HashPassword(pin.Trim());

        await _db.SetSettingAsync(KeyHash, Convert.ToBase64String(hashResult.Hash));
        await _db.SetSettingAsync(KeySalt, Convert.ToBase64String(hashResult.Salt));
        await _db.SetSettingAsync(KeyAlgorithm, hashResult.Algorithm);
        await _db.SetSettingAsync(KeyIterations, hashResult.Iterations.ToString());
        await _db.SetSettingAsync(KeyConfigured, "true");

        return true;
    }

    public async Task<bool> VerifyMasterPinAsync(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
            return false;

        string? hashB64 = await _db.GetSettingAsync(KeyHash);
        string? saltB64 = await _db.GetSettingAsync(KeySalt);
        string? algorithm = await _db.GetSettingAsync(KeyAlgorithm, PasswordService.DefaultAlgorithm);
        string? iterStr = await _db.GetSettingAsync(KeyIterations, PasswordService.DefaultIterations.ToString());

        if (string.IsNullOrWhiteSpace(hashB64) || string.IsNullOrWhiteSpace(saltB64))
            return false;

        try
        {
            byte[] storedHash = Convert.FromBase64String(hashB64);
            byte[] salt = Convert.FromBase64String(saltB64);
            int iterations = int.TryParse(iterStr, out var iters) ? iters : PasswordService.DefaultIterations;

            return _passwordService.VerifyPassword(pin.Trim(), storedHash, salt, algorithm ?? PasswordService.DefaultAlgorithm, iterations);
        }
        catch
        {
            return false;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> ChangeMasterPinAsync(string currentPin, string newPin)
    {
        bool isCurrentValid = await VerifyMasterPinAsync(currentPin);
        if (!isCurrentValid)
        {
            return (false, "Current Master PIN is incorrect.");
        }

        var validation = ValidatePin(newPin);
        if (!validation.IsValid)
        {
            return (false, validation.ErrorMessage);
        }

        await SetMasterPinAsync(newPin);
        return (true, null);
    }

    public async Task<(byte[] Hash, byte[] Salt, string Algorithm, int Iterations)?> GetMasterPinCredentialsAsync()
    {
        bool isConfigured = await IsMasterPinConfiguredAsync();
        if (!isConfigured) return null;

        string? hashB64 = await _db.GetSettingAsync(KeyHash);
        string? saltB64 = await _db.GetSettingAsync(KeySalt);
        string? algorithm = await _db.GetSettingAsync(KeyAlgorithm, PasswordService.DefaultAlgorithm);
        string? iterStr = await _db.GetSettingAsync(KeyIterations, PasswordService.DefaultIterations.ToString());

        if (string.IsNullOrWhiteSpace(hashB64) || string.IsNullOrWhiteSpace(saltB64))
            return null;

        try
        {
            byte[] storedHash = Convert.FromBase64String(hashB64);
            byte[] salt = Convert.FromBase64String(saltB64);
            int iterations = int.TryParse(iterStr, out var iters) ? iters : PasswordService.DefaultIterations;

            return (storedHash, salt, algorithm ?? PasswordService.DefaultAlgorithm, iterations);
        }
        catch
        {
            return null;
        }
    }
}
