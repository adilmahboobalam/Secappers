namespace SecApper.Security.Models;

public enum FolderStatus
{
    Unlocked,
    Locking,
    Locked,
    Unlocking,
    RecoveryRequired,
    Error,
    Inconsistent,
    Missing
}

public enum IconStatus
{
    Default,
    LockedIconApplied,
    Failed
}

public enum ProtectionMode
{
    None,
    Locked,
    Protected,
    LockedAndProtected
}

public enum EventSeverity
{
    Info,
    Warning,
    High,
    Critical
}

public enum JournalOperationType
{
    Lock,
    Unlock
}

public enum JournalStatus
{
    Started,
    AclBackupComplete,
    AclApplied,
    Verified,
    Completed,
    Interrupted,
    Failed,
    RolledBack
}
