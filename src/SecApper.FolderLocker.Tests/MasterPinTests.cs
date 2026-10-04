using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using SecApper.Security.Data;
using SecApper.Security.Models;
using SecApper.Security.Security;
using SecApper.Security.Services;
using Xunit;

namespace SecApper.FolderLocker.Tests;

public class MasterPinTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqliteDatabaseService _db;
    private readonly PasswordService _passwordService;
    private readonly MasterPinService _masterPinService;

    public MasterPinTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"secapper_pin_test_{Guid.NewGuid():N}.db");
        _db = new SqliteDatabaseService(_tempDbPath);
        _db.InitializeAsync().GetAwaiter().GetResult();
        _passwordService = new PasswordService();
        _masterPinService = new MasterPinService(_db, _passwordService);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDbPath))
                File.Delete(_tempDbPath);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task MasterPin_IsNotConfigured_Initially()
    {
        bool configured = await _masterPinService.IsMasterPinConfiguredAsync();
        Assert.False(configured);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("123", false)]
    [InlineData("1234", true)]
    [InlineData("98765432", true)]
    [InlineData("SecurePin123", true)]
    [InlineData("123456789012345678901234567890123", false)] // 33 chars > 32 max
    public void ValidatePin_EnforcesLengthConstraints(string pin, bool expectedValid)
    {
        var result = _masterPinService.ValidatePin(pin);
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public async Task SetMasterPinAsync_SetsAndVerifiesPin()
    {
        const string pin = "5849";
        await _masterPinService.SetMasterPinAsync(pin);

        bool isConfigured = await _masterPinService.IsMasterPinConfiguredAsync();
        Assert.True(isConfigured);

        bool verifiedValid = await _masterPinService.VerifyMasterPinAsync("5849");
        Assert.True(verifiedValid);

        bool verifiedInvalid = await _masterPinService.VerifyMasterPinAsync("0000");
        Assert.False(verifiedInvalid);
    }

    [Fact]
    public async Task ChangeMasterPinAsync_RequiresCorrectCurrentPin()
    {
        await _masterPinService.SetMasterPinAsync("1122");

        // Wrong current pin
        var wrongAttempt = await _masterPinService.ChangeMasterPinAsync("9999", "3344");
        Assert.False(wrongAttempt.Success);

        // Correct current pin
        var correctAttempt = await _masterPinService.ChangeMasterPinAsync("1122", "3344");
        Assert.True(correctAttempt.Success);

        // Old PIN no longer works
        Assert.False(await _masterPinService.VerifyMasterPinAsync("1122"));

        // New PIN works
        Assert.True(await _masterPinService.VerifyMasterPinAsync("3344"));
    }

    [Fact]
    public async Task FolderLockService_UnlocksWithMasterPin_WhenFolderPasswordDiffers()
    {
        string testFolder = Path.Combine(Path.GetTempPath(), $"secapper_folder_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testFolder);

        try
        {
            await _masterPinService.SetMasterPinAsync("Master99");

            var aclService = new AclService();
            var backupService = new PermissionBackupService(aclService, _db);
            var iconService = new FolderIconService();
            var lockService = new FolderLockService(_db, aclService, backupService, _passwordService, iconService, _masterPinService);

            // Create and register folder record
            const string folderPassword = "FolderSpecificPassword123!";
            var hashResult = _passwordService.HashPassword(folderPassword);
            var folder = new FolderRecord
            {
                FolderPath = testFolder,
                FolderName = Path.GetFileName(testFolder),
                Status = FolderStatus.Unlocked,
                PasswordHash = hashResult.Hash,
                PasswordSalt = hashResult.Salt,
                PasswordAlgorithm = hashResult.Algorithm,
                PasswordIterations = hashResult.Iterations
            };
            await _db.AddFolderAsync(folder);

            // Lock folder with folder-specific password
            var lockResult = await lockService.LockFolderAsync(folder.Id);
            Assert.True(lockResult.Success, lockResult.ErrorMessage);

            // Unlock attempt with incorrect password and incorrect master PIN should fail
            var failResult = await lockService.UnlockFolderAsync(folder.Id, "CompletelyWrong");
            Assert.False(failResult.Success);

            // Unlock attempt with MASTER PIN should succeed!
            var masterUnlockResult = await lockService.UnlockFolderAsync(folder.Id, "Master99");
            Assert.True(masterUnlockResult.Success, masterUnlockResult.ErrorMessage);

            // Verify status in DB is now Unlocked
            var updatedFolder = await _db.GetFolderByIdAsync(folder.Id);
            Assert.NotNull(updatedFolder);
            Assert.Equal(FolderStatus.Unlocked, updatedFolder!.Status);
        }
        finally
        {
            try
            {
                if (Directory.Exists(testFolder))
                    Directory.Delete(testFolder, true);
            }
            catch
            {
            }
        }
    }
}
