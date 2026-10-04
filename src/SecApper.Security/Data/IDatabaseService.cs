using System.Collections.Generic;
using System.Threading.Tasks;
using SecApper.Security.Models;

namespace SecApper.Security.Data;

public interface IDatabaseService
{
    Task InitializeAsync();
    string DatabasePath { get; }
    Task<int> GetSchemaVersionAsync();
    Task BackupDatabaseAsync(string destinationPath);

    // Folders
    Task<List<FolderRecord>> GetAllFoldersAsync();
    Task<FolderRecord?> GetFolderByIdAsync(string id);
    Task<FolderRecord?> GetFolderByPathAsync(string path);
    Task AddFolderAsync(FolderRecord folder);
    Task UpdateFolderAsync(FolderRecord folder);
    Task DeleteFolderAsync(string id);

    // Permission Backups
    Task AddPermissionBackupAsync(PermissionBackup backup);
    Task<PermissionBackup?> GetPermissionBackupByIdAsync(string id);
    Task<PermissionBackup?> GetLatestPermissionBackupForFolderAsync(string folderPath);
    Task DeletePermissionBackupAsync(string id);

    // Protection Policies
    Task<ProtectionPolicy?> GetPolicyForFolderAsync(string folderId);
    Task UpsertPolicyAsync(ProtectionPolicy policy);

    // Security Events
    Task AddSecurityEventAsync(SecurityEvent securityEvent);
    Task<List<SecurityEvent>> GetRecentSecurityEventsAsync(int limit = 100);
    Task<List<SecurityEvent>> GetEventsForFolderAsync(string folderId, int limit = 50);
    Task ClearEventsAsync();

    // Operation Journal (Crash Safety & Interrupted Operation Recovery)
    Task<string> StartJournalEntryAsync(string folderId, string folderPath, JournalOperationType opType, string? originalSddl = null, string? backupId = null);
    Task UpdateJournalStatusAsync(string journalId, JournalStatus status, string? errorMessage = null);
    Task<List<OperationJournalRecord>> GetIncompleteJournalEntriesAsync();
    Task<OperationJournalRecord?> GetLatestJournalForFolderAsync(string folderId);

    // App Settings
    Task<string?> GetSettingAsync(string key, string? defaultValue = null);
    Task SetSettingAsync(string key, string value);
}
