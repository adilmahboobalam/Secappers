using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SecApper.Security.Data;
using SecApper.Security.Models;
using SecApper.Security.Security;

namespace SecApper.Security.Ransomware;

public class RansomwareProtectionService : IRansomwareProtectionService
{
    private readonly IDatabaseService _databaseService;
    private readonly IFolderLockService _folderLockService;
    private readonly IThreatScoringService _threatScoringService;
    private readonly IProcessMonitorService _processMonitorService;

    private readonly ConcurrentDictionary<string, (FileSystemWatcher Watcher, ProtectionPolicy Policy, FolderRecord Folder)> _monitoredFolders = new();
    private readonly ConcurrentQueue<FileActivityEvent> _eventQueue = new();
    private readonly Timer _evaluationTimer;
    private bool _isDisposed;

    public event EventHandler<ThreatAlert>? ThreatDetected;

    public RansomwareProtectionService(
        IDatabaseService databaseService,
        IFolderLockService folderLockService,
        IThreatScoringService threatScoringService,
        IProcessMonitorService processMonitorService)
    {
        _databaseService = databaseService;
        _folderLockService = folderLockService;
        _threatScoringService = threatScoringService;
        _processMonitorService = processMonitorService;

        // Periodic evaluation every 1000ms
        _evaluationTimer = new Timer(EvaluateActivityEvents, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void StartMonitoringFolder(FolderRecord folder, ProtectionPolicy? policy = null)
    {
        if (string.IsNullOrWhiteSpace(folder.FolderPath) || !Directory.Exists(folder.FolderPath))
            return;

        // Stop existing if any
        StopMonitoringFolder(folder.Id);

        policy ??= new ProtectionPolicy { FolderId = folder.Id };

        try
        {
            var watcher = new FileSystemWatcher(folder.FolderPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            watcher.Changed += (s, e) => EnqueueEvent(folder.FolderPath, e.FullPath, e.ChangeType);
            watcher.Created += (s, e) => EnqueueEvent(folder.FolderPath, e.FullPath, e.ChangeType);
            watcher.Deleted += (s, e) => EnqueueEvent(folder.FolderPath, e.FullPath, e.ChangeType);
            watcher.Renamed += (s, e) => EnqueueEvent(folder.FolderPath, e.FullPath, e.ChangeType, e.OldFullPath);

            _monitoredFolders[folder.Id] = (watcher, policy, folder);
        }
        catch
        {
            // Non-critical if watcher fails to start
        }
    }

    private void EnqueueEvent(string folderPath, string fullPath, WatcherChangeTypes changeType, string? oldPath = null)
    {
        // Ignore desktop.ini
        if (Path.GetFileName(fullPath).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
            return;

        _eventQueue.Enqueue(new FileActivityEvent(folderPath, fullPath, changeType, DateTime.UtcNow, oldPath));
    }

    public void StopMonitoringFolder(string folderId)
    {
        if (_monitoredFolders.TryRemove(folderId, out var tuple))
        {
            try
            {
                tuple.Watcher.EnableRaisingEvents = false;
                tuple.Watcher.Dispose();
            }
            catch
            {
                // Cleanup
            }
        }
    }

    public void StopAllMonitoring()
    {
        foreach (var key in _monitoredFolders.Keys)
        {
            StopMonitoringFolder(key);
        }
    }

    public bool IsMonitoring(string folderId)
    {
        return _monitoredFolders.ContainsKey(folderId);
    }

    private void EvaluateActivityEvents(object? state)
    {
        if (_isDisposed || _eventQueue.IsEmpty) return;

        DateTime windowStart = DateTime.UtcNow.AddSeconds(-10);
        var recentEvents = new List<FileActivityEvent>();

        while (_eventQueue.TryDequeue(out var evt))
        {
            if (evt.Timestamp >= windowStart)
            {
                recentEvents.Add(evt);
            }
        }

        if (recentEvents.Count == 0) return;

        // Group by folder path
        var grouped = recentEvents.GroupBy(e => e.FolderPath, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            string folderPath = group.Key;
            var entry = _monitoredFolders.Values.FirstOrDefault(m => string.Equals(m.Folder.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase));
            if (entry.Watcher == null) continue;

            var assessment = _threatScoringService.EvaluateEvents(group, entry.Policy);
            if (assessment.Score >= 50)
            {
                // Suspicious or high threat detected
                var (pName, pid, pPath) = _processMonitorService.GetCurrentActiveProcessContext();

                var alert = new ThreatAlert(
                    entry.Folder.Id,
                    entry.Folder.FolderPath,
                    assessment,
                    pName,
                    pid,
                    pPath,
                    DateTime.UtcNow);

                // Raise event to UI
                ThreatDetected?.Invoke(this, alert);

                // Asynchronously record security event
                _ = Task.Run(async () =>
                {
                    await _databaseService.AddSecurityEventAsync(new SecurityEvent
                    {
                        FolderId = entry.Folder.Id,
                        EventType = "RansomwareSuspiciousActivity",
                        Severity = assessment.Level == ThreatLevel.Critical ? EventSeverity.Critical : EventSeverity.High,
                        ProcessName = pName,
                        ProcessPath = pPath,
                        ProcessId = pid,
                        Description = $"Ransomware indicators detected in {entry.Folder.FolderName}: {string.Join("; ", assessment.Indicators)}",
                        ActionTaken = "Alert dispatched to user"
                    });
                });
            }
        }
    }

    public async Task<int> LockAllFoldersAsync()
    {
        var folders = await _databaseService.GetAllFoldersAsync();
        int count = 0;
        foreach (var folder in folders)
        {
            if (folder.Status == FolderStatus.Unlocked)
            {
                var result = await _folderLockService.LockFolderAsync(folder.Id);
                if (result.Success)
                {
                    count++;
                }
            }
        }
        return count;
    }

    public async Task<int> PanicLockAllAsync()
    {
        var folders = await _databaseService.GetAllFoldersAsync();
        int count = 0;
        foreach (var folder in folders)
        {
            // Lock regardless of current status to ensure strict enforcement
            var result = await _folderLockService.LockFolderAsync(folder.Id);
            if (result.Success)
            {
                count++;
            }
        }

        await _databaseService.AddSecurityEventAsync(new SecurityEvent
        {
            EventType = "PanicLockTriggered",
            Severity = EventSeverity.Critical,
            Description = $"Emergency Panic Lock executed. Secured {count} folder(s).",
            ActionTaken = "Immediate lockdown of all registered folders"
        });

        return count;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _evaluationTimer.Dispose();
        StopAllMonitoring();
    }
}
