using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SecApper.Security.Models;

namespace SecApper.Security.Ransomware;

public enum ThreatLevel
{
    Low,
    Medium,
    High,
    Critical
}

public record ThreatAssessment(
    int Score,
    ThreatLevel Level,
    List<string> Indicators,
    int AffectedFilesCount);

public record FileActivityEvent(
    string FolderPath,
    string FilePath,
    WatcherChangeTypes ChangeType,
    DateTime Timestamp,
    string? OldFilePath = null);

public record ThreatAlert(
    string FolderId,
    string FolderPath,
    ThreatAssessment Assessment,
    string? ProcessName,
    int? ProcessId,
    string? ProcessPath,
    DateTime Timestamp);

public interface IThreatScoringService
{
    ThreatAssessment EvaluateEvents(IEnumerable<FileActivityEvent> events, ProtectionPolicy policy);
}

public interface IProcessMonitorService
{
    (string? ProcessName, int? ProcessId, string? ProcessPath) GetCurrentActiveProcessContext();
}

public interface IRansomwareProtectionService : IDisposable
{
    event EventHandler<ThreatAlert>? ThreatDetected;
    void StartMonitoringFolder(FolderRecord folder, ProtectionPolicy? policy = null);
    void StopMonitoringFolder(string folderId);
    void StopAllMonitoring();
    bool IsMonitoring(string folderId);
    Task<int> LockAllFoldersAsync();
    Task<int> PanicLockAllAsync();
}
