using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SecApper.FolderLocker.Services;
using SecApper.FolderLocker.ViewModels;
using SecApper.Security.Data;
using SecApper.Security.Models;
using SecApper.Security.Security;
using SecApper.Security.Services;
using Xunit;

namespace SecApper.FolderLocker.Tests;

public class UnlockAndAutoLockTests : IDisposable
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

    public UnlockAndAutoLockTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "SecApperAutoLock_" + Guid.NewGuid().ToString("N"));
        _targetFolder = Path.Combine(_testBaseDir, "ConfidentialFolder");
        Directory.CreateDirectory(_targetFolder);
        File.WriteAllText(Path.Combine(_targetFolder, "data.txt"), "Protected files content");

        _dbPath = Path.Combine(_testBaseDir, "test.db");
        _db = new SqliteDatabaseService(_dbPath);
        _passwordService = new PasswordService();
        _aclService = new AclService();
        _backupService = new PermissionBackupService(_aclService, _db);
        _iconService = new FolderIconService(Path.Combine(_testBaseDir, "test.ico"));
        _lockService = new FolderLockService(_db, _aclService, _backupService, _passwordService, _iconService);
    }

    [Fact]
    public void UnlockDialogViewModel_PropertiesAndValidation_BehaveCorrectly()
    {
        var vm = new UnlockDialogViewModel
        {
            FolderName = "ConfidentialFolder",
            FolderPath = _targetFolder,
            Password = "MySecurePassword123"
        };

        Assert.Equal("ConfidentialFolder", vm.FolderName);
        Assert.Equal(_targetFolder, vm.FolderPath);
        Assert.Equal("MySecurePassword123", vm.Password);
        Assert.False(vm.ShowPassword);

        vm.ShowPassword = true;
        Assert.True(vm.ShowPassword);

        vm.ErrorMessage = "Incorrect password.";
        Assert.Equal("Incorrect password.", vm.ErrorMessage);
    }

    [Fact]
    public void ExplorerWindowMonitorService_TrackAndUntrack_ManagesSessionsSafely()
    {
        using var monitor = new ExplorerWindowMonitorService(action => action());
        string folderId = Guid.NewGuid().ToString();

        // Should not throw
        monitor.TrackFolderWindow(folderId, _targetFolder);
        monitor.Start();

        // Untrack
        monitor.UntrackFolder(folderId);

        // Stop
        monitor.Stop();
    }

    [Fact]
    public async Task UnlockAndAutoLockCycle_RelocksFolderSuccessfully()
    {
        await _db.InitializeAsync();

        string password = "TestPasswordAutoLock#2026";
        var hashResult = _passwordService.HashPassword(password);

        var folder = new FolderRecord
        {
            FolderPath = _targetFolder,
            FolderName = "ConfidentialFolder",
            Status = FolderStatus.Unlocked,
            PasswordHash = hashResult.Hash,
            PasswordSalt = hashResult.Salt,
            PasswordAlgorithm = hashResult.Algorithm,
            PasswordIterations = hashResult.Iterations,
            ProtectionMode = ProtectionMode.LockedAndProtected
        };

        await _db.AddFolderAsync(folder);

        // 1. Lock the folder
        var lockResult = await _lockService.LockFolderAsync(folder.Id);
        Assert.True(lockResult.Success);

        var lockedFolder = await _db.GetFolderByIdAsync(folder.Id);
        Assert.NotNull(lockedFolder);
        Assert.Equal(FolderStatus.Locked, lockedFolder.Status);

        // 2. Unlock the folder with valid password
        var unlockResult = await _lockService.UnlockFolderAsync(folder.Id, password);
        Assert.True(unlockResult.Success);

        var unlockedFolder = await _db.GetFolderByIdAsync(folder.Id);
        Assert.NotNull(unlockedFolder);
        Assert.Equal(FolderStatus.Unlocked, unlockedFolder.Status);

        // 3. Simulate window close event triggering auto-lock
        bool autoLockInvoked = false;
        var autoLockTrigger = new TaskCompletionSource<bool>();

        Action<string, string> onWindowClosed = async (fId, fPath) =>
        {
            var f = await _db.GetFolderByIdAsync(fId);
            if (f != null && f.Status == FolderStatus.Unlocked)
            {
                var relockResult = await _lockService.LockFolderAsync(fId);
                autoLockInvoked = relockResult.Success;
                autoLockTrigger.SetResult(autoLockInvoked);
            }
        };

        // Fire simulated window closed
        onWindowClosed(folder.Id, folder.FolderPath);
        bool relocked = await autoLockTrigger.Task;

        Assert.True(relocked);
        Assert.True(autoLockInvoked);

        var finalFolder = await _db.GetFolderByIdAsync(folder.Id);
        Assert.NotNull(finalFolder);
        Assert.Equal(FolderStatus.Locked, finalFolder.Status);
    }

    public void Dispose()
    {
        try
        {
            // Restore ACL if still locked so temp dir can be cleaned
            _aclService.RestoreAcl(_targetFolder, "O:BAG:BAD:(A;;FA;;;WD)");
            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, true);
            }
        }
        catch
        {
        }
    }
}
