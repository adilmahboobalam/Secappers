using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SecApper.Security.Data;
using SecApper.Security.Models;
using SecApper.Security.Recovery;
using SecApper.Security.Security;
using SecApper.Security.Services;
using SecApper.Security.Updates;
using Xunit;

namespace SecApper.FolderLocker.Tests;

public class UpdateAndSecurityTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;

    public UpdateAndSecurityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SecApperUpdateTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "locker.db");
    }

    [Theory]
    [InlineData("https://github.com/adilmahboobalam/Secappers.git", "https://raw.githubusercontent.com/adilmahboobalam/Secappers/main/latest.json")]
    [InlineData("https://github.com/adilmahboobalam/Secappers", "https://raw.githubusercontent.com/adilmahboobalam/Secappers/main/latest.json")]
    [InlineData("https://github.com/adilmahboobalam/Secappers/blob/main/latest.json", "https://raw.githubusercontent.com/adilmahboobalam/Secappers/main/latest.json")]
    [InlineData("https://raw.githubusercontent.com/adilmahboobalam/Secappers/main/latest.json", "https://raw.githubusercontent.com/adilmahboobalam/Secappers/main/latest.json")]
    [InlineData("https://updates.secapper.com/latest.json", "https://raw.githubusercontent.com/adilmahboobalam/Secappers/main/latest.json")]
    [InlineData("", "https://raw.githubusercontent.com/adilmahboobalam/Secappers/main/latest.json")]
    [InlineData("file:///C:/test/latest.json", "file:///C:/test/latest.json")]
    public void NormalizeManifestUrl_ConvertsWebUrlsToRawEndpoints(string input, string expected)
    {
        string result = UpdateService.NormalizeManifestUrl(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SemVersion_Comparison_HandlesNumericProperly()
    {
        // Critical requirement 26: 1.10.0 must be newer than 1.9.0
        Assert.True(SemVersion.TryParse("1.10.0", out var v1_10));
        Assert.True(SemVersion.TryParse("1.9.0", out var v1_9));
        Assert.True(v1_10 > v1_9);
        Assert.False(v1_9 > v1_10);

        Assert.True(SemVersion.TryParse("1.0.1", out var v1_0_1));
        Assert.True(SemVersion.TryParse("1.0.0", out var v1_0_0));
        Assert.True(v1_0_1 > v1_0_0);

        Assert.True(SemVersion.TryParse("2.0.0", out var v2_0_0));
        Assert.True(v2_0_0 > v1_10);

        Assert.True(SemVersion.TryParse("1.2.3.4", out var v4parts));
        Assert.True(SemVersion.TryParse("1.2.3.5", out var v4partsB));
        Assert.True(v4partsB > v4parts);

        // Downgrade check
        Assert.False(v1_0_0 > v1_10);
    }

    [Fact]
    public void UpdateVerification_ValidChecksum_Succeeds()
    {
        string packagePath = Path.Combine(_tempDir, "test_update.exe");
        byte[] fakeContent = Encoding.UTF8.GetBytes("SecApper Installer Payload v1.2.0");
        File.WriteAllBytes(packagePath, fakeContent);

        string expectedSha;
        using (var sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(fakeContent);
            expectedSha = Convert.ToHexString(hash).ToLowerInvariant();
        }

        var updateInfo = new UpdateInfo
        {
            Version = "1.2.0",
            Sha256 = expectedSha,
            MinSupportedVersion = "1.0.0"
        };

        var db = new SqliteDatabaseService(_dbPath);
        var updateService = new UpdateService(db, "1.0.0");

        bool isValid = updateService.VerifyUpdatePackage(packagePath, updateInfo, out string? error);
        Assert.True(isValid, error);
        Assert.Null(error);
    }

    [Fact]
    public void UpdateVerification_TamperedChecksum_IsRejected()
    {
        string packagePath = Path.Combine(_tempDir, "test_update_tampered.exe");
        File.WriteAllBytes(packagePath, Encoding.UTF8.GetBytes("Original Payload"));

        var updateInfo = new UpdateInfo
        {
            Version = "1.2.0",
            Sha256 = "0000000000000000000000000000000000000000000000000000000000000000", // Wrong hash
            MinSupportedVersion = "1.0.0"
        };

        var db = new SqliteDatabaseService(_dbPath);
        var updateService = new UpdateService(db, "1.0.0");

        bool isValid = updateService.VerifyUpdatePackage(packagePath, updateInfo, out string? error);
        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("Checksum mismatch", error);
    }

    [Fact]
    public async Task CheckForUpdates_ResolvesLocalManifestAndDetectsNewVersion()
    {
        string manifestPath = Path.Combine(_tempDir, "latest.json");
        string json = @"
{
  ""version"": ""1.1.0"",
  ""downloadUrl"": ""file:///C:/test/installer.exe"",
  ""sha256"": ""abc"",
  ""releaseNotes"": ""Notes""
}";
        File.WriteAllText(manifestPath, json);

        var db = new SqliteDatabaseService(_dbPath);
        await db.InitializeAsync();
        await db.SetSettingAsync("UpdateManifestUrl", new Uri(manifestPath).AbsoluteUri);

        var updateService = new UpdateService(db, "1.0.0");
        var result = await updateService.CheckForUpdatesAsync(isManualCheck: true);

        Assert.True(result.IsUpdateAvailable);
        Assert.NotNull(result.Update);
        Assert.Equal("1.1.0", result.Update.Version);
    }

    [Fact]
    public async Task Database_MigrationAndBackup_PreservesDataAcrossVersions()
    {
        // 1. Initialize DB at current version
        var db = new SqliteDatabaseService(_dbPath);
        await db.InitializeAsync();

        // Add a folder record
        byte[] fakeHash = new byte[32];
        byte[] fakeSalt = new byte[16];
        RandomNumberGenerator.Fill(fakeHash);
        RandomNumberGenerator.Fill(fakeSalt);

        var folder = new FolderRecord
        {
            FolderPath = @"C:\TestFolder",
            FolderName = "TestFolder",
            Status = FolderStatus.Locked,
            PasswordHash = fakeHash,
            PasswordSalt = fakeSalt
        };
        await db.AddFolderAsync(folder);

        // Add setting
        await db.SetSettingAsync("AutoCheckUpdates", "true");

        // 2. Simulate update: create a new service instance targeting same file
        var dbAfterUpdate = new SqliteDatabaseService(_dbPath);
        await dbAfterUpdate.InitializeAsync();

        var retrieved = await dbAfterUpdate.GetFolderByIdAsync(folder.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(FolderStatus.Locked, retrieved.Status);
        Assert.Equal(fakeHash, retrieved.PasswordHash);

        string? setting = await dbAfterUpdate.GetSettingAsync("AutoCheckUpdates");
        Assert.Equal("true", setting);
    }

    [Fact]
    public async Task OperationJournal_CrashRecovery_DetectsIncompleteOperations()
    {
        var db = new SqliteDatabaseService(_dbPath);
        await db.InitializeAsync();

        byte[] fakeHash = new byte[32];
        byte[] fakeSalt = new byte[16];

        var folder = new FolderRecord
        {
            FolderPath = _tempDir,
            FolderName = "CrashRecoveryFolder",
            Status = FolderStatus.Locking, // Application crashed during locking!
            PasswordHash = fakeHash,
            PasswordSalt = fakeSalt
        };
        await db.AddFolderAsync(folder);

        // Log an interrupted journal entry
        await db.StartJournalEntryAsync(folder.Id, _tempDir, JournalOperationType.Lock);

        var acl = new AclService();
        var icon = new FolderIconService();
        var recoveryService = new RecoveryService(db, acl, icon);

        var issues = await recoveryService.ScanForInconsistenciesAsync();
        Assert.NotEmpty(issues);
        Assert.Contains(issues, i => i.Folder.Id == folder.Id);
    }

    [Fact]
    public async Task JournalLifecycle_TracksTransitionsAccurately()
    {
        var db = new SqliteDatabaseService(_dbPath);
        await db.InitializeAsync();

        byte[] fakeHash = new byte[32];
        byte[] fakeSalt = new byte[16];

        var folder = new FolderRecord
        {
            FolderPath = _tempDir,
            FolderName = "JournalTestFolder",
            Status = FolderStatus.Unlocked,
            PasswordHash = fakeHash,
            PasswordSalt = fakeSalt
        };
        await db.AddFolderAsync(folder);

        // Start
        string jId = await db.StartJournalEntryAsync(folder.Id, _tempDir, JournalOperationType.Lock);
        Assert.False(string.IsNullOrWhiteSpace(jId));

        var incomplete = await db.GetIncompleteJournalEntriesAsync();
        Assert.Single(incomplete);
        Assert.Equal(JournalStatus.Started, incomplete[0].Status);

        // Intermediate status
        await db.UpdateJournalStatusAsync(jId, JournalStatus.AclBackupComplete, "Backup SDDL stored");
        var latest = await db.GetLatestJournalForFolderAsync(folder.Id);
        Assert.NotNull(latest);
        Assert.Equal(JournalStatus.AclBackupComplete, latest.Status);

        // Complete
        await db.UpdateJournalStatusAsync(jId, JournalStatus.Completed, "Lock verified");
        var completedList = await db.GetIncompleteJournalEntriesAsync();
        Assert.Empty(completedList);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
        }
    }
}
