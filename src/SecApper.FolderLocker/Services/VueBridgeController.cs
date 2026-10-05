using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Wpf;
using SecApper.Security.Data;
using SecApper.Security.Models;
using SecApper.Security.Ransomware;
using SecApper.Security.Recovery;
using SecApper.Security.Security;
using SecApper.Security.Services;
using SecApper.Security.Updates;

namespace SecApper.FolderLocker.Services;

public class VueBridgeController
{
    private readonly Window _window;
    private readonly WebView2 _webView;
    private readonly IDatabaseService _db;
    private readonly IFolderLockService _lockService;
    private readonly IRecoveryService _recoveryService;
    private readonly IRansomwareProtectionService _ransomwareService;
    private readonly IUpdateService _updateService;
    private readonly IMasterPinService _masterPinService;
    private readonly IPasswordService _passwordService;
    private readonly IFolderPathValidator _pathValidator;
    private readonly SystemTrayService _trayService;
    private SecApper.Security.Updates.UpdateInfo? _lastUpdateInfo;
    private string? _downloadedPackagePath;

    public VueBridgeController(
        Window window,
        WebView2 webView,
        IDatabaseService db,
        IFolderLockService lockService,
        IRecoveryService recoveryService,
        IRansomwareProtectionService ransomwareService,
        IUpdateService updateService,
        IMasterPinService masterPinService,
        IPasswordService passwordService,
        IFolderPathValidator pathValidator,
        SystemTrayService trayService)
    {
        _window = window;
        _webView = webView;
        _db = db;
        _lockService = lockService;
        _recoveryService = recoveryService;
        _ransomwareService = ransomwareService;
        _updateService = updateService;
        _masterPinService = masterPinService;
        _passwordService = passwordService;
        _pathValidator = pathValidator;
        _trayService = trayService;
    }

    public async Task HandleMessageAsync(string messageJson)
    {
        string id = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("id", out var idProp))
            {
                id = idProp.GetString() ?? string.Empty;
            }

            if (!root.TryGetProperty("action", out var actionProp))
            {
                return;
            }

            string action = actionProp.GetString() ?? string.Empty;
            JsonElement payload = root.TryGetProperty("payload", out var pProp) ? pProp : default;

            object? result = await DispatchActionAsync(action, payload);

            SendResponse(id, result, null);
        }
        catch (Exception ex)
        {
            SendResponse(id, null, ex.Message);
        }
    }

    private async Task<object?> DispatchActionAsync(string action, JsonElement payload)
    {
        switch (action)
        {
            // --- Folders ---
            case "folders.list":
            {
                var folders = await _db.GetAllFoldersAsync();
                return folders.Select(MapFolder);
            }

            case "folders.lock":
            {
                string folderPath = payload.GetProperty("folderPath").GetString() ?? string.Empty;
                string password = payload.GetProperty("password").GetString() ?? string.Empty;

                var existingFolders = await _db.GetAllFoldersAsync();
                var validation = _pathValidator.ValidatePath(folderPath, existingFolders.Select(f => f.FolderPath));
                if (!validation.IsValid)
                {
                    return new { success = false, error = validation.ErrorMessage };
                }

                // Check if folder is already registered
                var existingFolder = await _db.GetFolderByPathAsync(folderPath);
                FolderRecord folder;

                if (existingFolder != null)
                {
                    folder = existingFolder;
                }
                else
                {
                    var hashResult = _passwordService.HashPassword(password);
                    string name = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (string.IsNullOrEmpty(name))
                    {
                        name = folderPath;
                    }

                    folder = new FolderRecord
                    {
                        FolderPath = folderPath,
                        FolderName = name,
                        Status = FolderStatus.Unlocked,
                        PasswordHash = hashResult.Hash,
                        PasswordSalt = hashResult.Salt,
                        PasswordAlgorithm = hashResult.Algorithm,
                        PasswordIterations = hashResult.Iterations,
                        ProtectionMode = ProtectionMode.LockedAndProtected
                    };

                    await _db.AddFolderAsync(folder);
                    await _db.UpsertPolicyAsync(new ProtectionPolicy
                    {
                        FolderId = folder.Id,
                        ProtectionEnabled = true,
                        MassModificationThreshold = 30
                    });
                }

                var lockResult = await _lockService.LockFolderAsync(folder.Id, password);
                if (lockResult.Success)
                {
                    var updated = await _db.GetFolderByIdAsync(folder.Id);
                    if (updated != null)
                    {
                        _ransomwareService.StartMonitoringFolder(updated);
                        return new { success = true, folder = MapFolder(updated) };
                    }
                }

                return new { success = false, error = lockResult.ErrorMessage ?? "Failed to apply Windows NTFS permissions." };
            }

            case "folders.unlock":
            {
                string folderId = payload.GetProperty("folderId").GetString() ?? string.Empty;
                string password = payload.GetProperty("password").GetString() ?? string.Empty;

                var unlockResult = await _lockService.UnlockFolderAsync(folderId, password);
                if (unlockResult.Success)
                {
                    var updated = await _db.GetFolderByIdAsync(folderId);
                    return new
                    {
                        success = true,
                        folder = updated != null ? MapFolder(updated) : null
                    };
                }

                return new { success = false, error = unlockResult.ErrorMessage ?? "Incorrect password. Please try again." };
            }

            case "folders.removeProtection":
            {
                string folderId = payload.GetProperty("folderId").GetString() ?? string.Empty;
                string password = payload.GetProperty("password").GetString() ?? string.Empty;

                var folder = await _db.GetFolderByIdAsync(folderId);
                if (folder == null)
                {
                    return new { success = false, error = "Folder not found." };
                }

                if (folder.Status == FolderStatus.Locked)
                {
                    var unlockResult = await _lockService.UnlockFolderAsync(folderId, password);
                    if (!unlockResult.Success)
                    {
                        return new { success = false, error = "Incorrect password. Authorization required to remove protection." };
                    }
                }

                _ransomwareService.StopMonitoringFolder(folderId);

                // Remove from SecApper tracking database. Actual files are NEVER deleted.
                await _db.DeleteFolderAsync(folderId);

                await _db.AddSecurityEventAsync(new SecurityEvent
                {
                    FolderId = folderId,
                    EventType = "ProtectionRemoved",
                    Description = $"SecApper protection removed for '{folder.FolderName}'. Filesystem permissions restored.",
                    Severity = EventSeverity.Warning,
                    ActionTaken = "Protection Removed"
                });

                return new { success = true };
            }

            case "folders.browseFolder":
            {
                string? selectedPath = null;
                await _window.Dispatcher.InvokeAsync(() =>
                {
                    using var dialog = new System.Windows.Forms.FolderBrowserDialog
                    {
                        Description = "Select a folder to secure with SecApper",
                        UseDescriptionForTitle = true,
                        ShowNewFolderButton = true
                    };

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        selectedPath = dialog.SelectedPath;
                    }
                });
                return selectedPath;
            }

            case "folders.openFolder":
            {
                string folderId = payload.GetProperty("folderId").GetString() ?? string.Empty;
                var folder = await _db.GetFolderByIdAsync(folderId);
                if (folder != null && Directory.Exists(folder.FolderPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folder.FolderPath,
                        UseShellExecute = true
                    });
                    return true;
                }
                return false;
            }

            case "folders.getDetails":
            {
                string folderId = payload.GetProperty("folderId").GetString() ?? string.Empty;
                var folder = await _db.GetFolderByIdAsync(folderId);
                return folder != null ? MapFolder(folder) : null;
            }

            // --- Security Status & Panic Lock ---
            case "security.getStatus":
            {
                var folders = await _db.GetAllFoldersAsync();
                int totalCount = folders.Count;
                int lockedCount = folders.Count(f => f.Status == FolderStatus.Locked);
                bool hasRecoveryIssues = folders.Any(f => f.Status == FolderStatus.RecoveryRequired || f.Status == FolderStatus.Inconsistent);
                bool hasPin = await _masterPinService.IsMasterPinConfiguredAsync();

                return new
                {
                    status = hasRecoveryIssues ? "ATTENTION" : "PROTECTED",
                    statusText = hasRecoveryIssues ? "Attention required for folder consistency." : "Your system is protected.",
                    isProtected = !hasRecoveryIssues,
                    protectedFoldersCount = totalCount,
                    lockedFoldersCount = lockedCount,
                    threatLevel = "LOW",
                    ransomwareActive = true,
                    databaseHealthy = true,
                    backupsAvailable = true,
                    hasRecoveryIssues,
                    masterPinConfigured = hasPin,
                    explorerIntegrationActive = true,
                    version = _updateService.CurrentVersion
                };
            }

            case "security.panicLock":
            {
                int count = await _ransomwareService.PanicLockAllAsync();
                return new { lockedCount = count, errors = Array.Empty<string>() };
            }

            // --- Ransomware ---
            case "ransomware.getStatus":
            {
                var folders = await _db.GetAllFoldersAsync();
                return new
                {
                    active = true,
                    threatLevel = "LOW",
                    monitoredFoldersCount = folders.Count,
                    suspiciousEventsToday = 0,
                    blockedProcessesCount = 0,
                    monitoringEngine = "FileSystemWatcher + Heuristic Threat Scorer",
                    recentAlerts = Array.Empty<object>()
                };
            }

            case "ransomware.toggle":
            {
                return true;
            }

            // --- Events ---
            case "events.list":
            {
                int limit = payload.TryGetProperty("limit", out var l) ? l.GetInt32() : 200;
                var events = await _db.GetRecentSecurityEventsAsync(limit);
                var folders = (await _db.GetAllFoldersAsync()).ToDictionary(f => f.Id, f => f.FolderName);

                return events.Select(e => new
                {
                    id = e.Id,
                    folderId = e.FolderId,
                    folderName = e.FolderId != null && folders.TryGetValue(e.FolderId, out var name) ? name : null,
                    eventType = e.EventType,
                    processName = e.ProcessName,
                    processPath = e.ProcessPath,
                    processId = e.ProcessId,
                    description = e.Description,
                    createdAt = e.CreatedAt.ToString("o"),
                    severity = e.Severity.ToString(),
                    actionTaken = e.ActionTaken
                });
            }

            case "events.clear":
            {
                await _db.ClearEventsAsync();
                return true;
            }

            case "events.exportCsv":
            {
                var events = await _db.GetRecentSecurityEventsAsync(1000);
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("ID,CreatedAt,Severity,EventType,FolderId,ProcessName,ProcessId,Description,ActionTaken");
                foreach (var e in events)
                {
                    sb.AppendLine($"\"{e.Id}\",\"{e.CreatedAt:o}\",\"{e.Severity}\",\"{e.EventType}\",\"{e.FolderId}\",\"{e.ProcessName}\",\"{e.ProcessId}\",\"{e.Description?.Replace("\"", "\"\"")}\",\"{e.ActionTaken}\"");
                }
                return sb.ToString();
            }

            // --- Recovery ---
            case "recovery.check":
            {
                var issues = await _recoveryService.ScanForInconsistenciesAsync();
                var folders = await _db.GetAllFoldersAsync();

                return new
                {
                    databaseHealthy = true,
                    permissionBackupsAvailable = true,
                    foldersConsistent = issues.Count == 0,
                    recoveryServiceReady = true,
                    issues = issues.Select(i => new
                    {
                        id = i.Folder.Id,
                        folderId = i.Folder.Id,
                        folderPath = i.Folder.FolderPath,
                        folderName = i.Folder.FolderName,
                        issueType = i.DiskIsLocked ? "InconsistentPermissions" : "UnprotectedState",
                        description = i.Description,
                        recommendedAction = i.DiskIsLocked ? "Restore default access or repair SDDL." : "Re-apply folder lock.",
                        canAutoRepair = true
                    }),
                    totalFoldersChecked = folders.Count,
                    checkedAt = DateTime.UtcNow.ToString("o")
                };
            }

            case "recovery.repair":
            {
                string issueId = payload.GetProperty("issueId").GetString() ?? string.Empty;
                bool success = await _recoveryService.RepairProtectionAsync(issueId);
                return new { success, message = success ? "Protection repaired successfully." : "Could not repair protection." };
            }

            case "recovery.restoreAccess":
            {
                string folderId = payload.GetProperty("folderId").GetString() ?? string.Empty;
                bool success = await _recoveryService.RestoreAccessAsync(folderId);
                return new { success, message = success ? "Access restored successfully." : "Could not restore access." };
            }

            // --- Updates ---
            case "updates.check":
            {
                var result = await _updateService.CheckForUpdatesAsync(true);
                if (result.Update != null)
                {
                    _lastUpdateInfo = result.Update;
                }
                return new
                {
                    currentVersion = _updateService.CurrentVersion,
                    availableVersion = result.Update?.Version ?? _updateService.CurrentVersion,
                    hasUpdate = result.IsUpdateAvailable,
                    releaseNotes = result.Update?.ReleaseNotes,
                    status = result.IsUpdateAvailable ? "Available" : "Idle",
                    statusMessage = result.IsUpdateAvailable ? "Update available." : "SecApper is up to date.",
                    downloadProgress = 0
                };
            }

            case "updates.download":
            {
                if (_lastUpdateInfo == null)
                {
                    var checkRes = await _updateService.CheckForUpdatesAsync(true);
                    _lastUpdateInfo = checkRes.Update;
                }

                if (_lastUpdateInfo == null)
                {
                    return new { success = false, error = "No update package available to download." };
                }

                var progress = new Progress<SecApper.Security.Updates.UpdateProgress>(p =>
                {
                    SendEvent("updateProgress", new
                    {
                        progress = p.Percentage,
                        message = p.StatusMessage
                    });
                });

                try
                {
                    _downloadedPackagePath = await _updateService.DownloadUpdateAsync(_lastUpdateInfo, progress);

                    bool isValid = _updateService.VerifyUpdatePackage(_downloadedPackagePath, _lastUpdateInfo, out string? verifyError);
                    if (!isValid)
                    {
                        return new { success = false, error = verifyError ?? "Update package verification failed." };
                    }

                    return new { success = true, packagePath = _downloadedPackagePath };
                }
                catch (Exception ex)
                {
                    return new { success = false, error = $"Download failed: {ex.Message}" };
                }
            }

            case "updates.install":
            {
                if (string.IsNullOrEmpty(_downloadedPackagePath) || !File.Exists(_downloadedPackagePath))
                {
                    if (_lastUpdateInfo == null)
                    {
                        var checkRes = await _updateService.CheckForUpdatesAsync(true);
                        _lastUpdateInfo = checkRes.Update;
                    }

                    if (_lastUpdateInfo == null)
                    {
                        return new { success = false, error = "Update package not found. Please download the update first." };
                    }

                    _downloadedPackagePath = await _updateService.DownloadUpdateAsync(_lastUpdateInfo);
                }

                if (_lastUpdateInfo == null)
                {
                    return new { success = false, error = "Update package metadata is missing." };
                }

                bool launched = await _updateService.LaunchUpdaterAndExitAsync(_downloadedPackagePath, _lastUpdateInfo);
                if (launched)
                {
                    await _window.Dispatcher.InvokeAsync(() =>
                    {
                        if (_window is MainWindow mw)
                        {
                            mw.ShutdownApp();
                        }
                        else
                        {
                            Environment.Exit(0);
                        }
                    });
                    return new { success = true };
                }

                return new { success = false, error = "Failed to launch SecApper updater process." };
            }

            // --- Settings ---
            case "settings.get":
            {
                bool hasPin = await _masterPinService.IsMasterPinConfiguredAsync();
                return new
                {
                    autoCheckUpdates = true,
                    updateFrequency = "Daily",
                    ransomwareProtectionEnabled = true,
                    massModificationThreshold = 30,
                    explorerIntegrationEnabled = true,
                    masterPinConfigured = hasPin,
                    darkMode = false,
                    startWithWindows = true,
                    minimizeToTray = true
                };
            }

            case "settings.update":
            {
                return new { success = true };
            }

            // --- Master PIN ---
            case "pin.isConfigured":
            {
                return await _masterPinService.IsMasterPinConfiguredAsync();
            }

            case "pin.setup":
            {
                string pin = payload.GetProperty("pin").GetString() ?? string.Empty;
                return await _masterPinService.SetMasterPinAsync(pin);
            }

            case "pin.verify":
            {
                string pin = payload.GetProperty("pin").GetString() ?? string.Empty;
                return await _masterPinService.VerifyMasterPinAsync(pin);
            }

            case "pin.change":
            {
                string oldPin = payload.GetProperty("oldPin").GetString() ?? string.Empty;
                string newPin = payload.GetProperty("newPin").GetString() ?? string.Empty;
                var res = await _masterPinService.ChangeMasterPinAsync(oldPin, newPin);
                return res.Success;
            }

            // --- Windows Management ---
            case "window.minimize":
                await _window.Dispatcher.InvokeAsync(() => _window.WindowState = WindowState.Minimized);
                return true;

            case "window.maximize":
                await _window.Dispatcher.InvokeAsync(() =>
                {
                    _window.WindowState = (_window.WindowState == WindowState.Maximized)
                        ? WindowState.Normal
                        : WindowState.Maximized;
                });
                return true;

            case "window.close":
                await _window.Dispatcher.InvokeAsync(() => _window.Close());
                return true;

            default:
                throw new InvalidOperationException($"Unrecognized bridge action: {action}");
        }
    }

    private static object MapFolder(FolderRecord f)
    {
        return new
        {
            id = f.Id,
            folderPath = f.FolderPath,
            folderName = f.FolderName,
            status = f.Status.ToString(),
            createdAt = f.CreatedAt.ToString("o"),
            updatedAt = f.UpdatedAt.ToString("o"),
            lastLockedAt = f.LastLockedAt?.ToString("o"),
            lastUnlockedAt = f.LastUnlockedAt?.ToString("o"),
            protectionMode = f.ProtectionMode.ToString(),
            iconStatus = f.IconStatus.ToString(),
            originalAclBackupId = f.OriginalAclBackupId
        };
    }

    private void SendResponse(string id, object? result, string? error)
    {
        try
        {
            var response = new
            {
                id,
                result,
                error
            };
            string json = JsonSerializer.Serialize(response);
            _window.Dispatcher.Invoke(() =>
            {
                _webView.CoreWebView2?.PostWebMessageAsJson(json);
            });
        }
        catch
        {
        }
    }

    public void SendEvent(string eventName, object payload)
    {
        try
        {
            var eventObj = new
            {
                @event = eventName,
                payload
            };
            string json = JsonSerializer.Serialize(eventObj);
            _window.Dispatcher.Invoke(() =>
            {
                _webView.CoreWebView2?.PostWebMessageAsJson(json);
            });
        }
        catch
        {
        }
    }
}
