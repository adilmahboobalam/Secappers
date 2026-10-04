using System;
using System.IO;
using System.Threading.Tasks;
using SecApper.Security.Data;
using SecApper.Security.Models;
using SecApper.Security.Services;

namespace SecApper.Security.Security;

public class FolderLockService : IFolderLockService
{
    private readonly IDatabaseService _databaseService;
    private readonly IAclService _aclService;
    private readonly IPermissionBackupService _backupService;
    private readonly IPasswordService _passwordService;
    private readonly IFolderIconService _iconService;
    private readonly IMasterPinService? _masterPinService;

    public FolderLockService(
        IDatabaseService databaseService,
        IAclService aclService,
        IPermissionBackupService backupService,
        IPasswordService passwordService,
        IFolderIconService iconService,
        IMasterPinService? masterPinService = null)
    {
        _databaseService = databaseService;
        _aclService = aclService;
        _backupService = backupService;
        _passwordService = passwordService;
        _iconService = iconService;
        _masterPinService = masterPinService;
    }

    public async Task<LockResult> LockFolderAsync(string folderId, string? password = null)
    {
        var folder = await _databaseService.GetFolderByIdAsync(folderId);
        if (folder == null)
            return new LockResult(false, "Folder record not found.", null);

        if (!Directory.Exists(folder.FolderPath))
        {
            folder.Status = FolderStatus.Missing;
            await _databaseService.UpdateFolderAsync(folder);
            return new LockResult(false, $"Folder does not exist on disk: {folder.FolderPath}", null);
        }

        // If a new password is provided, update password hash
        if (!string.IsNullOrEmpty(password))
        {
            var hashResult = _passwordService.HashPassword(password);
            folder.PasswordHash = hashResult.Hash;
            folder.PasswordSalt = hashResult.Salt;
            folder.PasswordAlgorithm = hashResult.Algorithm;
            folder.PasswordIterations = hashResult.Iterations;
        }

        // STATE: Transition to LOCKING
        folder.Status = FolderStatus.Locking;
        await _databaseService.UpdateFolderAsync(folder);

        string journalId = await _databaseService.StartJournalEntryAsync(
            folder.Id,
            folder.FolderPath,
            JournalOperationType.Lock);

        PermissionBackup? backup = null;

        try
        {
            // Step 1: Backup current permissions before locking
            try
            {
                backup = await _backupService.CreateBackupAsync(folder.FolderPath);
                await _databaseService.UpdateJournalStatusAsync(journalId, JournalStatus.AclBackupComplete);
            }
            catch (Exception ex)
            {
                folder.Status = FolderStatus.Unlocked;
                await _databaseService.UpdateFolderAsync(folder);
                await _databaseService.UpdateJournalStatusAsync(journalId, JournalStatus.Failed, $"Backup failed: {ex.Message}");

                await _databaseService.AddSecurityEventAsync(new SecurityEvent
                {
                    FolderId = folder.Id,
                    EventType = "AclBackupFailed",
                    Severity = EventSeverity.High,
                    Description = $"Failed to create permission backup for {folder.FolderPath}: {ex.Message}",
                    ActionTaken = "Aborted lock operation to prevent data lock-out."
                });

                return new LockResult(false, $"Failed to create ACL backup: {ex.Message}", null);
            }

            // Step 2: Apply visual indicator icon (Written before ACL lock is applied)
            bool iconApplied = _iconService.SetLockedIcon(folder.FolderPath);

            // Step 3: Apply Windows ACL Lock
            var lockOp = _aclService.ApplyLockAcl(folder.FolderPath);
            if (!lockOp.Success)
            {
                // Rollback icon and state
                _iconService.RestoreDefaultIcon(folder.FolderPath);
                folder.Status = FolderStatus.Unlocked;
                await _databaseService.UpdateFolderAsync(folder);
                await _databaseService.UpdateJournalStatusAsync(journalId, JournalStatus.Failed, lockOp.ErrorMessage);

                return new LockResult(false, lockOp.ErrorMessage ?? "Failed to apply Windows ACL lock.", null);
            }

            await _databaseService.UpdateJournalStatusAsync(journalId, JournalStatus.AclApplied);

            // Step 4: VERIFY the lock (Mandatory Verification)
            bool isVerifiedLocked = _aclService.VerifyIsLocked(folder.FolderPath);
            if (!isVerifiedLocked)
            {
                // Rollback immediately to original permissions and icon
                _aclService.RestoreAcl(folder.FolderPath, backup.OriginalSddl);
                _iconService.RestoreDefaultIcon(folder.FolderPath);

                folder.Status = FolderStatus.Unlocked;
                await _databaseService.UpdateFolderAsync(folder);
                await _databaseService.UpdateJournalStatusAsync(journalId, JournalStatus.RolledBack, "Lock verification failed; restored original ACL.");

                await _databaseService.AddSecurityEventAsync(new SecurityEvent
                {
                    FolderId = folder.Id,
                    EventType = "LockVerificationFailed",
                    Severity = EventSeverity.Critical,
                    Description = $"Lock verification failed for {folder.FolderPath}. Rolled back to original permissions.",
                    ActionTaken = "Permission rollback to Unlocked"
                });

                return new LockResult(false, "Lock verification failed: access restriction was not enforced by the filesystem. Rolled back.", null);
            }

            await _databaseService.UpdateJournalStatusAsync(journalId, JournalStatus.Verified);

            // Step 5: Transition to LOCKED
            folder.Status = FolderStatus.Locked;
            folder.LastLockedAt = DateTime.UtcNow;
            folder.OriginalAclBackupId = backup.Id;
            folder.IconStatus = iconApplied ? IconStatus.LockedIconApplied : IconStatus.Failed;
            folder.ProtectionMode = ProtectionMode.Locked;

            await _databaseService.UpdateFolderAsync(folder);
            await _databaseService.UpdateJournalStatusAsync(journalId, JournalStatus.Completed);

            // Step 6: Log security event
            await _databaseService.AddSecurityEventAsync(new SecurityEvent
            {
                FolderId = folder.Id,
                EventType = "FolderLocked",
                Severity = EventSeverity.Info,
                Description = $"Folder '{folder.FolderName}' locked successfully with Windows ACL protection.",
                ActionTaken = "Access Denied applied to authenticated users."
            });

            return new LockResult(true, null, backup.Id);
        }
        catch (Exception ex)
        {
            // In case of unexpected crash or exception during locking, attempt safe rollback
            if (backup != null)
            {
                try
                {
                    _aclService.RestoreAcl(folder.FolderPath, backup.OriginalSddl);
                }
                catch { }
            }

            folder.Status = FolderStatus.Unlocked;
            try { await _databaseService.UpdateFolderAsync(folder); } catch { }
            try { await _databaseService.UpdateJournalStatusAsync(journalId, JournalStatus.Failed, ex.Message); } catch { }

            return new LockResult(false, $"Unexpected error locking folder: {ex.Message}", null);
        }
    }

    public async Task<UnlockResult> UnlockFolderAsync(string folderId, string password)
    {
        var folder = await _databaseService.GetFolderByIdAsync(folderId);
        if (folder == null)
            return new UnlockResult(false, "Folder record not found.");

        if (!Directory.Exists(folder.FolderPath))
        {
            folder.Status = FolderStatus.Missing;
            await _databaseService.UpdateFolderAsync(folder);
            return new UnlockResult(false, $"Folder does not exist on disk: {folder.FolderPath}");
        }

        // Verify password with constant-time equality
        bool passwordMatches = _passwordService.VerifyPassword(
            password,
            folder.PasswordHash,
            folder.PasswordSalt,
            folder.PasswordAlgorithm,
            folder.PasswordIterations);

        // Fallback: Verify if input matches the Master Security PIN
        if (!passwordMatches && _masterPinService != null)
        {
            passwordMatches = await _masterPinService.VerifyMasterPinAsync(password);
        }

        if (!passwordMatches)
        {
            await _databaseService.AddSecurityEventAsync(new SecurityEvent
            {
                FolderId = folder.Id,
                EventType = "UnlockFailed",
                Severity = EventSeverity.Warning,
                Description = $"Failed unlock attempt with incorrect password for folder '{folder.FolderName}'.",
                ActionTaken = "Access refused"
            });
            return new UnlockResult(false, "Incorrect password. Access denied.");
        }

        // Step 1: Locate backup SDDL
        PermissionBackup? backup = null;
        if (!string.IsNullOrEmpty(folder.OriginalAclBackupId))
        {
            backup = await _backupService.GetBackupAsync(folder.OriginalAclBackupId);
        }
        if (backup == null)
        {
            backup = await _databaseService.GetLatestPermissionBackupForFolderAsync(folder.FolderPath);
        }

        if (backup == null)
        {
            folder.Status = FolderStatus.RecoveryRequired;
            await _databaseService.UpdateFolderAsync(folder);
            return new UnlockResult(false, "Could not find a valid permission backup to restore original ACL. Recovery is required.");
        }

        // STATE: Transition to UNLOCKING
        folder.Status = FolderStatus.Unlocking;
        await _databaseService.UpdateFolderAsync(folder);

        string journalId = await _databaseService.StartJournalEntryAsync(
            folder.Id,
            folder.FolderPath,
            JournalOperationType.Unlock,
            backup.OriginalSddl,
            backup.Id);

        try
        {
            // Step 2: Restore original ACL
            var restoreOp = _aclService.RestoreAcl(folder.FolderPath, backup.OriginalSddl);
            if (!restoreOp.Success)
            {
                folder.Status = FolderStatus.RecoveryRequired;
                await _databaseService.UpdateFolderAsync(folder);
                await _databaseService.UpdateJournalStatusAsync(journalId, JournalStatus.Failed, restoreOp.ErrorMessage);

                return new UnlockResult(false, restoreOp.ErrorMessage ?? "Failed to restore original folder permissions. Recovery required.");
            }

            await _databaseService.UpdateJournalStatusAsync(journalId, JournalStatus.AclApplied);

            // Step 3: VERIFY access is restored
            bool isAccessible = _aclService.VerifyIsAccessible(folder.FolderPath);
            if (!isAccessible)
            {
                folder.Status = FolderStatus.RecoveryRequired;
                await _databaseService.UpdateFolderAsync(folder);
                await _databaseService.UpdateJournalStatusAsync(journalId, JournalStatus.Failed, "Restored ACL but folder remains inaccessible.");

                return new UnlockResult(false, "Folder permissions were restored, but Windows access verification failed. Recovery required.");
            }

            await _databaseService.UpdateJournalStatusAsync(journalId, JournalStatus.Verified);

            // Step 4: Restore normal folder icon
            _iconService.RestoreDefaultIcon(folder.FolderPath);

            // Step 5: Transition to UNLOCKED
            folder.Status = FolderStatus.Unlocked;
            folder.LastUnlockedAt = DateTime.UtcNow;
            folder.IconStatus = IconStatus.Default;
            folder.ProtectionMode = ProtectionMode.None;

            await _databaseService.UpdateFolderAsync(folder);
            await _databaseService.UpdateJournalStatusAsync(journalId, JournalStatus.Completed);

            // Step 6: Log security event
            await _databaseService.AddSecurityEventAsync(new SecurityEvent
            {
                FolderId = folder.Id,
                EventType = "FolderUnlocked",
                Severity = EventSeverity.Info,
                Description = $"Folder '{folder.FolderName}' unlocked successfully. Permissions restored.",
                ActionTaken = "Original ACL restored"
            });

            return new UnlockResult(true, null);
        }
        catch (Exception ex)
        {
            folder.Status = FolderStatus.RecoveryRequired;
            try { await _databaseService.UpdateFolderAsync(folder); } catch { }
            try { await _databaseService.UpdateJournalStatusAsync(journalId, JournalStatus.Failed, ex.Message); } catch { }

            return new UnlockResult(false, $"Error during unlocking: {ex.Message}. Recovery required.");
        }
    }

    public async Task<bool> VerifyFolderStateAsync(string folderId)
    {
        var folder = await _databaseService.GetFolderByIdAsync(folderId);
        if (folder == null) return false;

        if (!Directory.Exists(folder.FolderPath))
        {
            if (folder.Status != FolderStatus.Missing)
            {
                folder.Status = FolderStatus.Missing;
                await _databaseService.UpdateFolderAsync(folder);
            }
            return false;
        }

        bool isCurrentlyLockedOnDisk = _aclService.VerifyIsLocked(folder.FolderPath);

        if (folder.Status == FolderStatus.Locked && !isCurrentlyLockedOnDisk)
        {
            folder.Status = FolderStatus.Inconsistent;
            await _databaseService.UpdateFolderAsync(folder);
            return false;
        }

        if (folder.Status == FolderStatus.Unlocked && isCurrentlyLockedOnDisk)
        {
            folder.Status = FolderStatus.Inconsistent;
            await _databaseService.UpdateFolderAsync(folder);
            return false;
        }

        return true;
    }

    public async Task<AclOperationResult> EmergencyUnlockAsync(string folderId)
    {
        var folder = await _databaseService.GetFolderByIdAsync(folderId);
        if (folder == null)
            return new AclOperationResult(false, "Folder record not found.");

        var backup = await _databaseService.GetLatestPermissionBackupForFolderAsync(folder.FolderPath);
        if (backup == null)
            return new AclOperationResult(false, "No backup SDDL found for folder.");

        var result = _aclService.RestoreAcl(folder.FolderPath, backup.OriginalSddl);
        if (result.Success)
        {
            _iconService.RestoreDefaultIcon(folder.FolderPath);
            folder.Status = FolderStatus.Unlocked;
            folder.LastUnlockedAt = DateTime.UtcNow;
            folder.IconStatus = IconStatus.Default;
            await _databaseService.UpdateFolderAsync(folder);
        }

        return result;
    }
}
