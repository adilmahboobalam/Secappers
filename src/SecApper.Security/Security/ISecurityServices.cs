using System.Threading.Tasks;
using SecApper.Security.Models;

namespace SecApper.Security.Security;

public record AclOperationResult(bool Success, string? ErrorMessage);
public record LockResult(bool Success, string? ErrorMessage, string? BackupId);
public record UnlockResult(bool Success, string? ErrorMessage);

public interface IAclService
{
    string GetCurrentSddl(string folderPath);
    AclOperationResult ApplyLockAcl(string folderPath);
    AclOperationResult RestoreAcl(string folderPath, string sddl);
    bool VerifyIsLocked(string folderPath);
    bool VerifyIsAccessible(string folderPath);
}

public interface IPermissionBackupService
{
    Task<PermissionBackup> CreateBackupAsync(string folderPath);
    Task<bool> RestoreBackupAsync(string backupId, string folderPath);
    Task<PermissionBackup?> GetBackupAsync(string backupId);
}

public interface IFolderLockService
{
    Task<LockResult> LockFolderAsync(string folderId, string? password = null);
    Task<UnlockResult> UnlockFolderAsync(string folderId, string password);
    Task<bool> VerifyFolderStateAsync(string folderId);
    Task<AclOperationResult> EmergencyUnlockAsync(string folderId);
}
