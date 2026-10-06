using System.Threading.Tasks;

namespace SecApper.Security.Security;

public interface IMasterPinService
{
    Task<bool> IsMasterPinConfiguredAsync();
    Task<bool> SetMasterPinAsync(string pin);
    Task<bool> VerifyMasterPinAsync(string pin);
    Task<(bool Success, string? ErrorMessage)> ChangeMasterPinAsync(string currentPin, string newPin);
    (bool IsValid, string? ErrorMessage) ValidatePin(string pin);
    Task<(byte[] Hash, byte[] Salt, string Algorithm, int Iterations)?> GetMasterPinCredentialsAsync();
}
