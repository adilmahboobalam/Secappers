using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SecApper.Security.Data;
using SecApper.Security.Models;
using SecApper.Security.Ransomware;
using SecApper.Security.Security;
using SecApper.Security.Services;
using Xunit;

namespace SecApper.FolderLocker.Tests;

public class PasswordServiceTests
{
    private readonly PasswordService _passwordService = new();

    [Fact]
    public void HashPassword_ProducesDifferentSaltsAndHashesForSamePassword()
    {
        var result1 = _passwordService.HashPassword("SecretPassword123!");
        var result2 = _passwordService.HashPassword("SecretPassword123!");

        Assert.NotNull(result1.Hash);
        Assert.NotNull(result1.Salt);
        Assert.Equal(32, result1.Hash.Length);
        Assert.Equal(32, result1.Salt.Length);

        // Salt and hash must be unique per invocation
        Assert.False(result1.Salt.SequenceEqual(result2.Salt));
        Assert.False(result1.Hash.SequenceEqual(result2.Hash));
    }

    [Fact]
    public void VerifyPassword_ValidPassword_ReturnsTrue()
    {
        string password = "MySecureFolderPass#2026";
        var hashResult = _passwordService.HashPassword(password);

        bool isValid = _passwordService.VerifyPassword(
            password,
            hashResult.Hash,
            hashResult.Salt,
            hashResult.Algorithm,
            hashResult.Iterations);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_InvalidPassword_ReturnsFalse()
    {
        string password = "CorrectPassword123";
        var hashResult = _passwordService.HashPassword(password);

        bool isValid = _passwordService.VerifyPassword(
            "WrongPassword456",
            hashResult.Hash,
            hashResult.Salt,
            hashResult.Algorithm,
            hashResult.Iterations);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("12345", false)]
    [InlineData("123456", true)]
    [InlineData("ValidStrongPassword99!", true)]
    public void ValidatePasswordStrength_ValidatesCorrectly(string pwd, bool expectedValid)
    {
        var (isValid, _) = _passwordService.ValidatePasswordStrength(pwd);
        Assert.Equal(expectedValid, isValid);
    }
}

public class FolderPathValidatorTests
{
    private readonly FolderPathValidator _validator;
    private readonly string _tempTestDir;

    public FolderPathValidatorTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "SecApperValidatorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempTestDir);
        _validator = new FolderPathValidator();
    }

    [Fact]
    public void ValidatePath_EmptyPath_ReturnsInvalid()
    {
        var result = _validator.ValidatePath("", Enumerable.Empty<string>());
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidatePath_NonExistentPath_ReturnsInvalid()
    {
        var result = _validator.ValidatePath(@"C:\ThisFolderDoesNotExist_" + Guid.NewGuid().ToString("N"), Enumerable.Empty<string>());
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidatePath_SystemWindowsDirectory_ReturnsInvalid()
    {
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var result = _validator.ValidatePath(winDir, Enumerable.Empty<string>());
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidatePath_ProgramFilesDirectory_ReturnsInvalid()
    {
        string pfDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var result = _validator.ValidatePath(pfDir, Enumerable.Empty<string>());
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidatePath_DriveRoot_ReturnsInvalid()
    {
        var result = _validator.ValidatePath(@"C:\", Enumerable.Empty<string>());
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidatePath_ValidFolder_ReturnsValid()
    {
        var result = _validator.ValidatePath(_tempTestDir, Enumerable.Empty<string>());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidatePath_AlreadyRegistered_ReturnsInvalid()
    {
        var registered = new[] { _tempTestDir };
        var result = _validator.ValidatePath(_tempTestDir, registered);
        Assert.False(result.IsValid);
        Assert.Contains("already registered", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePath_NestedInsideRegistered_ReturnsInvalid()
    {
        string subDir = Path.Combine(_tempTestDir, "SubFolder");
        Directory.CreateDirectory(subDir);

        var registered = new[] { _tempTestDir };
        var result = _validator.ValidatePath(subDir, registered);
        Assert.False(result.IsValid);
        Assert.Contains("already inside protected folder", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePath_ParentOfRegistered_ReturnsInvalid()
    {
        string subDir = Path.Combine(_tempTestDir, "SubFolder");
        Directory.CreateDirectory(subDir);

        var registered = new[] { subDir };
        var result = _validator.ValidatePath(_tempTestDir, registered);
        Assert.False(result.IsValid);
        Assert.Contains("already protected", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}

public class ThreatScoringServiceTests
{
    private readonly ThreatScoringService _threatScorer = new();
    private readonly ProtectionPolicy _policy = new()
    {
        MassModificationThreshold = 20,
        DeleteProtection = true,
        RenameProtection = true,
        ExtensionChangeProtection = true
    };

    [Fact]
    public void EvaluateEvents_NormalActivity_ReturnsLowScore()
    {
        var events = new[]
        {
            new FileActivityEvent(@"D:\Data", @"D:\Data\doc1.txt", WatcherChangeTypes.Changed, DateTime.UtcNow),
            new FileActivityEvent(@"D:\Data", @"D:\Data\doc2.txt", WatcherChangeTypes.Changed, DateTime.UtcNow)
        };

        var assessment = _threatScorer.EvaluateEvents(events, _policy);
        Assert.Equal(ThreatLevel.Low, assessment.Level);
        Assert.True(assessment.Score < 25);
    }

    [Fact]
    public void EvaluateEvents_RansomwareExtension_ElevatesThreatScore()
    {
        var events = new[]
        {
            new FileActivityEvent(@"D:\Data", @"D:\Data\file1.txt.locked", WatcherChangeTypes.Created, DateTime.UtcNow)
        };

        var assessment = _threatScorer.EvaluateEvents(events, _policy);
        Assert.True(assessment.Score >= 50);
        Assert.Contains(assessment.Indicators, i => i.Contains(".locked", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EvaluateEvents_MassModifications_TriggersIndicator()
    {
        var events = Enumerable.Range(1, 25).Select(i =>
            new FileActivityEvent(@"D:\Data", $@"D:\Data\file{i}.txt", WatcherChangeTypes.Changed, DateTime.UtcNow)
        ).ToList();

        var assessment = _threatScorer.EvaluateEvents(events, _policy);
        Assert.True(assessment.Score >= 35);
        Assert.Contains(assessment.Indicators, i => i.Contains("Mass file modification", StringComparison.OrdinalIgnoreCase));
    }
}

public class DatabaseServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteDatabaseService _db;

    public DatabaseServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "SecApperTestDb_" + Guid.NewGuid().ToString("N") + ".db");
        _db = new SqliteDatabaseService(_dbPath);
    }

    [Fact]
    public async Task InitializeAndFolderCrud_WorksCorrectly()
    {
        await _db.InitializeAsync();

        var folder = new FolderRecord
        {
            FolderPath = @"C:\TestFolder_" + Guid.NewGuid().ToString("N"),
            FolderName = "TestFolder",
            Status = FolderStatus.Unlocked,
            PasswordHash = new byte[] { 1, 2, 3, 4 },
            PasswordSalt = new byte[] { 5, 6, 7, 8 },
            PasswordAlgorithm = "PBKDF2-SHA256",
            PasswordIterations = 310000
        };

        // Add
        await _db.AddFolderAsync(folder);

        // Get by ID
        var fetched = await _db.GetFolderByIdAsync(folder.Id);
        Assert.NotNull(fetched);
        Assert.Equal(folder.FolderName, fetched.FolderName);
        Assert.Equal(folder.FolderPath, fetched.FolderPath);
        Assert.True(folder.PasswordHash.SequenceEqual(fetched.PasswordHash));

        // Update
        fetched.Status = FolderStatus.Locked;
        fetched.LastLockedAt = DateTime.UtcNow;
        await _db.UpdateFolderAsync(fetched);

        var updated = await _db.GetFolderByIdAsync(folder.Id);
        Assert.NotNull(updated);
        Assert.Equal(FolderStatus.Locked, updated.Status);

        // Security event log
        await _db.AddSecurityEventAsync(new SecurityEvent
        {
            FolderId = folder.Id,
            EventType = "FolderLocked",
            Severity = EventSeverity.Info,
            Description = "Test lock event",
            ActionTaken = "Locked"
        });

        var events = await _db.GetEventsForFolderAsync(folder.Id);
        Assert.Single(events);
        Assert.Equal("FolderLocked", events[0].EventType);

        // Delete
        await _db.DeleteFolderAsync(folder.Id);
        var deleted = await _db.GetFolderByIdAsync(folder.Id);
        Assert.Null(deleted);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Ignore test cleanup error
        }
    }
}
