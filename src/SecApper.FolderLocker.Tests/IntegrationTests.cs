using System;
using System.IO;
using System.Security.AccessControl;
using System.Threading.Tasks;
using SecApper.Security.Data;
using SecApper.Security.Models;
using SecApper.Security.Ransomware;
using SecApper.Security.Recovery;
using SecApper.Security.Security;
using SecApper.Security.Services;
using Xunit;

namespace SecApper.FolderLocker.Tests;

public class FolderLockIntegrationTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly string _targetFolder;
    private readonly string _dbPath;
    private readonly SqliteDatabaseService _db;
    private readonly PasswordService _passwordService;
    private readonly AclService _aclService;
    private readonly PermissionBackupService _backupService;
    private readonly FolderIconService _iconService;
    private readonly FolderLockService _lockService;
    private readonly RecoveryService _recoveryService;

    public FolderLockIntegrationTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "SecApperInteg_" + Guid.NewGuid().ToString("N"));
        _targetFolder = Path.Combine(_testBaseDir, "PrivateData");
        Directory.CreateDirectory(_targetFolder);

        // Populate with files
        File.WriteAllText(Path.Combine(_targetFolder, "document.txt"), "Sensitive offline data content");
        File.WriteAllText(Path.Combine(_targetFolder, "secrets.json"), "{\"offline\": true}");

        _dbPath = Path.Combine(_testBaseDir, "test_locker.db");
        _db = new SqliteDatabaseService(_dbPath);
        _passwordService = new PasswordService();
        _aclService = new AclService();
        _backupService = new PermissionBackupService(_aclService, _db);
        _iconService = new FolderIconService(Path.Combine(_testBaseDir, "test_locked.ico"));
        _lockService = new FolderLockService(_db, _aclService, _backupService, _passwordService, _iconService);
        _recoveryService = new RecoveryService(_db, _aclService, _iconService);
    }

    [Fact]
    public async Task CompleteLockUnlockCycle_SucceedsWithIntegrity()
    {
        await _db.InitializeAsync();

        string password = "TestPassword2026!";
        var hashResult = _passwordService.HashPassword(password);

        var folder = new FolderRecord
        {
            FolderPath = _targetFolder,
            FolderName = "PrivateData",
            Status = FolderStatus.Unlocked,
            PasswordHash = hashResult.Hash,
            PasswordSalt = hashResult.Salt,
            PasswordAlgorithm = hashResult.Algorithm,
            PasswordIterations = hashResult.Iterations
        };

        await _db.AddFolderAsync(folder);

        // 1. Initial State: Folder is accessible
        Assert.True(_aclService.VerifyIsAccessible(_targetFolder));

        // 2. Lock Folder
        var lockResult = await _lockService.LockFolderAsync(folder.Id);
        Assert.True(lockResult.Success, lockResult.ErrorMessage);

        var lockedFolder = await _db.GetFolderByIdAsync(folder.Id);
        Assert.NotNull(lockedFolder);
        Assert.Equal(FolderStatus.Locked, lockedFolder.Status);
        Assert.NotNull(lockedFolder.OriginalAclBackupId);

        // 3. Verify on disk: ACL denies normal enumeration
        bool isLockedOnDisk = _aclService.VerifyIsLocked(_targetFolder);
        Assert.True(isLockedOnDisk);

        // 4. Attempt unlock with WRONG password: Must fail and stay locked
        var wrongUnlock = await _lockService.UnlockFolderAsync(folder.Id, "WrongPassword!");
        Assert.False(wrongUnlock.Success);
        Assert.True(_aclService.VerifyIsLocked(_targetFolder));

        // 5. Attempt unlock with CORRECT password: Must succeed
        var correctUnlock = await _lockService.UnlockFolderAsync(folder.Id, password);
        Assert.True(correctUnlock.Success, correctUnlock.ErrorMessage);

        var unlockedFolder = await _db.GetFolderByIdAsync(folder.Id);
        Assert.NotNull(unlockedFolder);
        Assert.Equal(FolderStatus.Unlocked, unlockedFolder.Status);

        // 6. Verify filesystem access restored and file contents intact
        Assert.True(_aclService.VerifyIsAccessible(_targetFolder));
        string content = File.ReadAllText(Path.Combine(_targetFolder, "document.txt"));
        Assert.Equal("Sensitive offline data content", content);
    }

    [Fact]
    public async Task RecoveryService_DetectsInconsistencyAndRestores()
    {
        await _db.InitializeAsync();

        string password = "RecoveryPassword123!";
        var hashResult = _passwordService.HashPassword(password);

        var folder = new FolderRecord
        {
            FolderPath = _targetFolder,
            FolderName = "PrivateData",
            Status = FolderStatus.Locked, // DB says Locked
            PasswordHash = hashResult.Hash,
            PasswordSalt = hashResult.Salt
        };

        await _db.AddFolderAsync(folder);

        // But actual disk is currently UNLOCKED
        var issues = await _recoveryService.ScanForInconsistenciesAsync();
        Assert.Single(issues);
        Assert.Equal(folder.Id, issues[0].Folder.Id);
        Assert.False(issues[0].DiskIsLocked);

        // Now repair protection
        bool repaired = await _recoveryService.RepairProtectionAsync(folder.Id);
        Assert.True(repaired);

        // Verify disk is now locked
        Assert.True(_aclService.VerifyIsLocked(_targetFolder));
    }

    [Fact]
    public async Task Section59_SecApperTestDirectoryWorkflow()
    {
        string folder = @"C:\SecApperTest";
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "test1.txt"), "SecApper offline security test file 1");

        await _db.InitializeAsync();

        string password = "SecApperTestMasterPassword2026!";
        var hashResult = _passwordService.HashPassword(password);

        var record = new FolderRecord
        {
            FolderPath = folder,
            FolderName = "SecApperTest",
            Status = FolderStatus.Unlocked,
            PasswordHash = hashResult.Hash,
            PasswordSalt = hashResult.Salt,
            PasswordAlgorithm = hashResult.Algorithm,
            PasswordIterations = hashResult.Iterations
        };

        await _db.AddFolderAsync(record);

        // Lock
        var lockRes = await _lockService.LockFolderAsync(record.Id);
        Assert.True(lockRes.Success, lockRes.ErrorMessage);

        // Verify ACL denies access
        Assert.True(_aclService.VerifyIsLocked(folder));

        // Wrong password fails
        var wrongUnlock = await _lockService.UnlockFolderAsync(record.Id, "WrongPass");
        Assert.False(wrongUnlock.Success);

        // Correct password restores access
        var correctUnlock = await _lockService.UnlockFolderAsync(record.Id, password);
        Assert.True(correctUnlock.Success, correctUnlock.ErrorMessage);

        // Verify files intact
        Assert.True(_aclService.VerifyIsAccessible(folder));
        Assert.True(File.Exists(Path.Combine(folder, "test1.txt")));
        Assert.Equal("SecApper offline security test file 1", File.ReadAllText(Path.Combine(folder, "test1.txt")).Trim());

        // Cleanup DB record
        await _db.DeleteFolderAsync(record.Id);
    }

    public void Dispose()
    {
        try
        {
            // Reset permissions if locked so cleanup doesn't fail
            try
            {
                DirectoryInfo dInfo = new(_targetFolder);
                DirectorySecurity sec = dInfo.GetAccessControl();
                var rules = sec.GetAccessRules(true, false, typeof(System.Security.Principal.SecurityIdentifier));
                foreach (FileSystemAccessRule rule in rules)
                {
                    if (rule.AccessControlType == AccessControlType.Deny)
                    {
                        sec.RemoveAccessRule(rule);
                    }
                }
                dInfo.SetAccessControl(sec);
            }
            catch
            {
            }

            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, true);
            }
        }
        catch
        {
            // Best effort test cleanup
        }
    }
}
