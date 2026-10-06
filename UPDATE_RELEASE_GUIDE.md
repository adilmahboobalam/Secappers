# SecApper Folder Locker — Architecture & Release Guide

## 1. Security Engine Architecture

SecApper Folder Locker is a Windows 10/11 offline-first folder security solution.

### Core Guarantees
1. **Local Security**: 100% offline functionality. Locking, unlocking, password verification, NTFS ACLs, ransomware monitoring, and recovery never require an internet connection.
2. **Real NTFS Enforcement**: Protection is enforced directly by the Windows filesystem kernel using Discretionary Access Control Lists (DACLs). A folder is never considered locked merely because of a database flag or icon.
3. **Preserved Administrative Access**: The `SYSTEM` and `Administrators` security principals are deliberately preserved with full control so Windows background services and administrative recovery tools remain operational.
4. **Resilient Unlocking & Rollback**: Before modifying ACLs, the original security descriptor is captured in Security Descriptor Definition Language (SDDL) and committed to SQLite (`C:\ProgramData\SecApper\FolderLocker\locker.db`). If any step of locking fails, an automatic rollback restores the original ACL.
5. **Crash Safety & Operation Journal**: All state transitions (`UNLOCKED` -> `LOCKING` -> `LOCKED` -> `UNLOCKING` -> `RECOVERY_REQUIRED`) are tracked in an `OperationJournal` table. On startup, `RecoveryService` checks for any interrupted or incomplete operations and flags them for recovery.

---

## 2. Live Online Auto-Update System

### Overview
SecApper includes an in-app updater that checks for signed releases, verifies cryptographic SHA-256 checksums, downloads updates without blocking the UI, and applies updates via a standalone bootstrapper (`SecApper.Updater.exe`).

### Critical Update Guarantees
- **Data Preservation**: User data and protected folder metadata located at `C:\ProgramData\SecApper\FolderLocker\locker.db` are **never** deleted, modified, or overwritten by the updater or installer.
- **Lock State Preservation**: Locked folders remain locked through the update process because NTFS ACLs are independent of application binary files.
- **Atomic File Replacement with Rollback**: `SecApper.Updater.exe` backs up existing binaries to a timestamped backup directory prior to copying new files. If any error occurs during replacement, previous binaries are restored automatically.
- **Semantic Versioning**: All versions are parsed with `SemVersion`, ensuring that `1.10.0` is recognized as newer than `1.9.0`.

---

## 3. Developer Release Workflow

To publish a new version of SecApper Folder Locker:

### Step 1: Update Application Version
Update version attributes in:
- `src/SecApper.FolderLocker/SecApper.FolderLocker.csproj` (`<Version>1.1.0</Version>`)
- `src/SecApper.Security/SecApper.Security.csproj` (`<Version>1.1.0</Version>`)
- `installer/SecApperFolderLocker.iss` (`#define MyAppVersion "1.1.0"`)

### Step 2: Build & Run Tests
Ensure all 30 unit and integration tests pass:
```powershell
dotnet test SecApperFolderLocker.sln
```

### Step 3: Publish Binaries
Publish Release builds of both the main application and the updater:
```powershell
dotnet publish src\SecApper.FolderLocker\SecApper.FolderLocker.csproj -c Release -r win-x64 --self-contained true -o publish
dotnet publish src\SecApper.Updater\SecApper.Updater.csproj -c Release -r win-x64 --self-contained true -o publish
```

### Step 4: Generate Package & SHA-256 Checksum
Create the installer package or update zip file from `publish/`, then generate its SHA-256 hash:
```powershell
Get-FileHash -Algorithm SHA256 publish\SecApperFolderLockerSetup.exe
```

### Step 5: Update the Update Manifest
Upload the installer package to your HTTPS hosting endpoint and update the manifest JSON (`latest.json`):
```json
{
  "version": "1.1.0",
  "releaseDate": "2026-10-04",
  "downloadUrl": "https://updates.secapper.com/releases/1.1.0/SecApperFolderLockerSetup.exe",
  "sha256": "<SHA256_HEX_STRING>",
  "mandatory": false,
  "minSupportedVersion": "1.0.0",
  "releaseNotes": "Enhanced NTFS ACL restoration engine and automated crash recovery journal."
}
```

---

## 4. Verification Test Matrix

| Test Case | Expected Result | Status |
|-----------|-----------------|--------|
| Lock folder on disk | Deny ACE applied, folder inaccessible to user | PASSED |
| Unlock with correct password | Original SDDL restored, files intact | PASSED |
| Unlock with wrong password | Access denied, folder stays locked | PASSED |
| Crash during Lock/Unlock | `RecoveryService` flags folder as `RecoveryRequired` | PASSED |
| Semantic Versioning comparison | `1.10.0 > 1.9.0`, `1.0.1 > 1.0.0` | PASSED |
| Corrupted/Tampered update package | SHA-256 mismatch detected, package discarded | PASSED |
| Database schema migration | `PRAGMA user_version` stepped, pre-migration backup created | PASSED |
| Offline operation without internet | All locker and ransomware features functional | PASSED |
