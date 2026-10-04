using System;

namespace SecApper.Security.Models;

public class FolderRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FolderPath { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public FolderStatus Status { get; set; } = FolderStatus.Unlocked;
    public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
    public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();
    public string PasswordAlgorithm { get; set; } = "PBKDF2-SHA256";
    public int PasswordIterations { get; set; } = 310000;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLockedAt { get; set; }
    public DateTime? LastUnlockedAt { get; set; }
    public string? OriginalAclBackupId { get; set; }
    public IconStatus IconStatus { get; set; } = IconStatus.Default;
    public ProtectionMode ProtectionMode { get; set; } = ProtectionMode.Locked;
}

public class SecurityEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? FolderId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? ProcessName { get; set; }
    public string? ProcessPath { get; set; }
    public int? ProcessId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public EventSeverity Severity { get; set; } = EventSeverity.Info;
    public string ActionTaken { get; set; } = string.Empty;
}

public class ProtectionPolicy
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FolderId { get; set; } = string.Empty;
    public bool ProtectionEnabled { get; set; } = true;
    public bool BlockUnknownProcesses { get; set; } = false;
    public int MassModificationThreshold { get; set; } = 30; // 30 files in window
    public bool DeleteProtection { get; set; } = true;
    public bool RenameProtection { get; set; } = true;
    public bool ExtensionChangeProtection { get; set; } = true;
}

public class PermissionBackup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FolderPath { get; set; } = string.Empty;
    public string OriginalSddl { get; set; } = string.Empty;
    public string? OwnerSid { get; set; }
    public string? GroupSid { get; set; }
    public bool AreAccessRulesProtected { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string HashVerification { get; set; } = string.Empty;
}

public class OperationJournalRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FolderId { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public JournalOperationType OperationType { get; set; }
    public JournalStatus Status { get; set; } = JournalStatus.Started;
    public string? OriginalSddl { get; set; }
    public string? BackupId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AppSettingRecord
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
