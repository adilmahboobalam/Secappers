using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SecApper.Security.Data;
using SecApper.Security.Models;

namespace SecApper.Security.Security;

public class PermissionBackupService : IPermissionBackupService
{
    private readonly IAclService _aclService;
    private readonly IDatabaseService _databaseService;

    public PermissionBackupService(IAclService aclService, IDatabaseService databaseService)
    {
        _aclService = aclService;
        _databaseService = databaseService;
    }

    public async Task<PermissionBackup> CreateBackupAsync(string folderPath)
    {
        string sddl = _aclService.GetCurrentSddl(folderPath);
        string hash = ComputeHash(sddl);

        var backup = new PermissionBackup
        {
            Id = Guid.NewGuid().ToString("N"),
            FolderPath = folderPath,
            OriginalSddl = sddl,
            CreatedAt = DateTime.UtcNow,
            HashVerification = hash,
            AreAccessRulesProtected = false
        };

        await _databaseService.AddPermissionBackupAsync(backup);
        return backup;
    }

    public async Task<bool> RestoreBackupAsync(string backupId, string folderPath)
    {
        var backup = await _databaseService.GetPermissionBackupByIdAsync(backupId);
        if (backup == null)
        {
            // Try fetching latest backup for folder
            backup = await _databaseService.GetLatestPermissionBackupForFolderAsync(folderPath);
            if (backup == null) return false;
        }

        // Verify integrity
        string computedHash = ComputeHash(backup.OriginalSddl);
        if (!string.Equals(computedHash, backup.HashVerification, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Backup integrity check failed: SDDL appears to have been modified or corrupted.");
        }

        var result = _aclService.RestoreAcl(folderPath, backup.OriginalSddl);
        return result.Success;
    }

    public async Task<PermissionBackup?> GetBackupAsync(string backupId)
    {
        return await _databaseService.GetPermissionBackupByIdAsync(backupId);
    }

    private static string ComputeHash(string input)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
