using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SecApper.Security.Data;
using SecApper.Security.Models;
using SecApper.Security.Security;
using SecApper.Security.Services;

namespace SecApper.Security.Recovery;

public class RecoveryService : IRecoveryService
{
    private readonly IDatabaseService _databaseService;
    private readonly IAclService _aclService;
    private readonly IFolderIconService _iconService;

    public RecoveryService(
        IDatabaseService databaseService,
        IAclService aclService,
        IFolderIconService iconService)
    {
        _databaseService = databaseService;
        _aclService = aclService;
        _iconService = iconService;
    }

    public async Task<List<RecoveryIssue>> ScanForInconsistenciesAsync()
    {
        var issues = new List<RecoveryIssue>();

        // 1. Check Operation Journal for interrupted operations (crash safety)
        try
        {
            var incompleteJournals = await _databaseService.GetIncompleteJournalEntriesAsync();
            foreach (var journal in incompleteJournals)
            {
                var folder = await _databaseService.GetFolderByIdAsync(journal.FolderId);
                if (folder != null)
                {
                    folder.Status = FolderStatus.RecoveryRequired;
                    await _databaseService.UpdateFolderAsync(folder);
                    await _databaseService.UpdateJournalStatusAsync(journal.Id, JournalStatus.Interrupted, "Interrupted operation detected on startup.");

                    bool diskLocked = Directory.Exists(folder.FolderPath) && _aclService.VerifyIsLocked(folder.FolderPath);

                    issues.Add(new RecoveryIssue(
                        folder,
                        $"Application crashed or was terminated during {journal.OperationType.ToString().ToUpperInvariant()} at {journal.StartedAt:g} UTC. Security recovery is required.",
                        DiskIsLocked: diskLocked,
                        FolderExists: Directory.Exists(folder.FolderPath)));
                }
            }
        }
        catch
        {
            // Best effort journal check
        }

        // 2. Scan registered folders and compare database vs physical NTFS filesystem state
        var folders = await _databaseService.GetAllFoldersAsync();

        foreach (var folder in folders)
        {
            // Skip if already reported via journal
            if (issues.Exists(i => i.Folder.Id == folder.Id))
                continue;

            if (!Directory.Exists(folder.FolderPath))
            {
                if (folder.Status != FolderStatus.Missing)
                {
                    folder.Status = FolderStatus.Missing;
                    await _databaseService.UpdateFolderAsync(folder);
                }

                issues.Add(new RecoveryIssue(
                    folder,
                    $"The folder no longer exists at path: {folder.FolderPath}",
                    DiskIsLocked: false,
                    FolderExists: false));
                continue;
            }

            bool diskIsLocked = _aclService.VerifyIsLocked(folder.FolderPath);

            if (folder.Status == FolderStatus.Locking || folder.Status == FolderStatus.Unlocking)
            {
                // Incomplete transient state left over from unexpected process shutdown
                folder.Status = FolderStatus.RecoveryRequired;
                await _databaseService.UpdateFolderAsync(folder);

                issues.Add(new RecoveryIssue(
                    folder,
                    $"Folder was in a transient state ({folder.Status}) when the application closed. Recovery is required to ensure consistent permissions.",
                    DiskIsLocked: diskIsLocked,
                    FolderExists: true));
            }
            else if (folder.Status == FolderStatus.Locked && !diskIsLocked)
            {
                // Inconsistency: database expects locked, but folder is currently accessible
                folder.Status = FolderStatus.Inconsistent;
                await _databaseService.UpdateFolderAsync(folder);

                issues.Add(new RecoveryIssue(
                    folder,
                    "Database records indicate this folder is LOCKED, but Windows filesystem permissions currently allow access.",
                    DiskIsLocked: false,
                    FolderExists: true));
            }
            else if (folder.Status == FolderStatus.Unlocked && diskIsLocked)
            {
                // Inconsistency: database expects unlocked, but folder permissions are locked
                folder.Status = FolderStatus.Inconsistent;
                await _databaseService.UpdateFolderAsync(folder);

                issues.Add(new RecoveryIssue(
                    folder,
                    "Database records indicate this folder is UNLOCKED, but Windows filesystem permissions are currently restricting access.",
                    DiskIsLocked: true,
                    FolderExists: true));
            }
        }

        return issues;
    }

    public async Task<bool> RepairProtectionAsync(string folderId)
    {
        var folder = await _databaseService.GetFolderByIdAsync(folderId);
        if (folder == null || !Directory.Exists(folder.FolderPath)) return false;

        var op = _aclService.ApplyLockAcl(folder.FolderPath);
        if (!op.Success) return false;

        bool verified = _aclService.VerifyIsLocked(folder.FolderPath);
        if (!verified) return false;

        _iconService.SetLockedIcon(folder.FolderPath);

        folder.Status = FolderStatus.Locked;
        folder.IconStatus = IconStatus.LockedIconApplied;
        folder.LastLockedAt = DateTime.UtcNow;
        await _databaseService.UpdateFolderAsync(folder);

        await _databaseService.AddSecurityEventAsync(new SecurityEvent
        {
            FolderId = folder.Id,
            EventType = "ProtectionRepaired",
            Severity = EventSeverity.Warning,
            Description = $"Inconsistency resolved: Protection reapplied to folder '{folder.FolderName}'.",
            ActionTaken = "ACL re-locked and icon updated"
        });

        return true;
    }

    public async Task<bool> RestoreAccessAsync(string folderId)
    {
        var folder = await _databaseService.GetFolderByIdAsync(folderId);
        if (folder == null || !Directory.Exists(folder.FolderPath)) return false;

        var backup = await _databaseService.GetLatestPermissionBackupForFolderAsync(folder.FolderPath);
        if (backup == null) return false;

        var op = _aclService.RestoreAcl(folder.FolderPath, backup.OriginalSddl);
        if (!op.Success) return false;

        bool verified = _aclService.VerifyIsAccessible(folder.FolderPath);
        if (!verified) return false;

        _iconService.RestoreDefaultIcon(folder.FolderPath);

        folder.Status = FolderStatus.Unlocked;
        folder.IconStatus = IconStatus.Default;
        folder.LastUnlockedAt = DateTime.UtcNow;
        await _databaseService.UpdateFolderAsync(folder);

        await _databaseService.AddSecurityEventAsync(new SecurityEvent
        {
            FolderId = folder.Id,
            EventType = "AccessRestored",
            Severity = EventSeverity.Warning,
            Description = $"Inconsistency resolved: Access restored to folder '{folder.FolderName}' using permission backup.",
            ActionTaken = "Original permissions restored"
        });

        return true;
    }
}
