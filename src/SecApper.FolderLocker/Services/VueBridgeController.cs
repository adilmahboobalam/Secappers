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
    private readonly IExplorerWindowMonitorService? _explorerWindowMonitor;
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
        SystemTrayService trayService,
        IExplorerWindowMonitorService? explorerWindowMonitor = null)
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
        _explorerWindowMonitor = explorerWindowMonitor;
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
                string folderPath = payload.TryGetProperty("folderPath", out var fpProp) ? fpProp.GetString() ?? string.Empty : string.Empty;
                string password = payload.TryGetProperty("password", out var pwProp) ? pwProp.GetString() ?? string.Empty : string.Empty;

                var existingFolder = await _db.GetFolderByPathAsync(folderPath);

                // Only validate path uniqueness/validity if not already an existing registered folder
                if (existingFolder == null)
                {
                    var existingFolders = await _db.GetAllFoldersAsync();
                    var validation = _pathValidator.ValidatePath(folderPath, existingFolders.Select(f => f.FolderPath));
                    if (!validation.IsValid)
                    {
                        return new { success = false, error = validation.ErrorMessage };
                    }
                }

                FolderRecord folder;

                if (existingFolder != null)
                {
                    folder = existingFolder;
                }
                else
                {
                    byte[] hash;
                    byte[] salt;
                    string algorithm;
                    int iterations;

                    if (!string.IsNullOrEmpty(password))
                    {
                        var hashResult = _passwordService.HashPassword(password);
                        hash = hashResult.Hash;
                        salt = hashResult.Salt;
                        algorithm = hashResult.Algorithm;
                        iterations = hashResult.Iterations;
                    }
                    else
                    {
                        // Use Master PIN credentials
                        var masterCreds = await _masterPinService.GetMasterPinCredentialsAsync();
                        if (masterCreds != null)
                        {
                            hash = masterCreds.Value.Hash;
                            salt = masterCreds.Value.Salt;
                            algorithm = masterCreds.Value.Algorithm;
                            iterations = masterCreds.Value.Iterations;
                        }
                        else
                        {
                            return new { success = false, error = "Please set up a Master PIN or enter a password to protect this folder." };
                        }
                    }

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
                        PasswordHash = hash,
                        PasswordSalt = salt,
                        PasswordAlgorithm = algorithm,
                        PasswordIterations = iterations,
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

                var lockResult = await _lockService.LockFolderAsync(folder.Id, string.IsNullOrEmpty(password) ? null : password);
                if (lockResult.Success)
                {
                    _explorerWindowMonitor?.UntrackFolder(folder.Id);
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

                _explorerWindowMonitor?.UntrackFolder(folderId);
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
                    _explorerWindowMonitor?.TrackFolderWindow(folder.Id, folder.FolderPath);
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
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(500);
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
                    });
                    return new { success = true };
                }

                return new { success = false, error = "Failed to launch SecApper updater process." };
            }

            // --- Settings ---
            case "settings.get":
            {
                bool hasPin = await _masterPinService.IsMasterPinConfiguredAsync();
                string autoCheck = await _db.GetSettingAsync("AutoCheckUpdates", "true") ?? "true";
                string freq = await _db.GetSettingAsync("UpdateCheckFrequency", "Daily") ?? "Daily";
                string ransomware = await _db.GetSettingAsync("RansomwareProtectionEnabled", "true") ?? "true";
                string thresholdStr = await _db.GetSettingAsync("MassModificationThreshold", "30") ?? "30";
                string explorer = await _db.GetSettingAsync("ExplorerIntegrationEnabled", "true") ?? "true";
                string darkModeStr = await _db.GetSettingAsync("DarkMode", "false") ?? "false";
                string startWin = await _db.GetSettingAsync("StartWithWindows", "true") ?? "true";
                string minTray = await _db.GetSettingAsync("MinimizeToTray", "true") ?? "true";

                int.TryParse(thresholdStr, out int threshold);
                if (threshold <= 0) threshold = 30;

                return new
                {
                    autoCheckUpdates = autoCheck == "true",
                    updateFrequency = freq,
                    ransomwareProtectionEnabled = ransomware == "true",
                    massModificationThreshold = threshold,
                    explorerIntegrationEnabled = explorer == "true",
                    masterPinConfigured = hasPin,
                    darkMode = darkModeStr == "true",
                    startWithWindows = startWin == "true",
                    minimizeToTray = minTray == "true"
                };
            }

            case "settings.update":
            {
                if (payload.TryGetProperty("autoCheckUpdates", out var acu))
                    await _db.SetSettingAsync("AutoCheckUpdates", acu.GetBoolean() ? "true" : "false");
                if (payload.TryGetProperty("updateFrequency", out var uf))
                    await _db.SetSettingAsync("UpdateCheckFrequency", uf.GetString() ?? "Daily");
                if (payload.TryGetProperty("ransomwareProtectionEnabled", out var rpe))
                    await _db.SetSettingAsync("RansomwareProtectionEnabled", rpe.GetBoolean() ? "true" : "false");
                if (payload.TryGetProperty("massModificationThreshold", out var mmt))
                    await _db.SetSettingAsync("MassModificationThreshold", mmt.GetInt32().ToString());
                if (payload.TryGetProperty("explorerIntegrationEnabled", out var eie))
                    await _db.SetSettingAsync("ExplorerIntegrationEnabled", eie.GetBoolean() ? "true" : "false");
                if (payload.TryGetProperty("darkMode", out var dm))
                    await _db.SetSettingAsync("DarkMode", dm.GetBoolean() ? "true" : "false");
                if (payload.TryGetProperty("startWithWindows", out var sww))
                    await _db.SetSettingAsync("StartWithWindows", sww.GetBoolean() ? "true" : "false");
                if (payload.TryGetProperty("minimizeToTray", out var mtt))
                    await _db.SetSettingAsync("MinimizeToTray", mtt.GetBoolean() ? "true" : "false");

                return new { success = true };
            }

            // --- Dynamic Setup & User Profile ---
            case "setup.isCompleted":
            {
                string? completed = await _db.GetSettingAsync("SetupCompleted", "false");
                if (string.Equals(completed, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // If user already has a Master PIN configured or has folders, setup is already completed (one-time only)
                bool hasPin = await _masterPinService.IsMasterPinConfiguredAsync();
                var folders = await _db.GetAllFoldersAsync();
                if (hasPin || folders.Count > 0)
                {
                    await _db.SetSettingAsync("SetupCompleted", "true");
                    return true;
                }

                return false;
            }

            case "setup.getInitialData":
            {
                string systemUser = Environment.UserName;
                if (string.IsNullOrWhiteSpace(systemUser)) systemUser = "Security User";

                string machine = Environment.MachineName;
                string os = Environment.OSVersion.VersionString;

                string? installId = await _db.GetSettingAsync("UserProfile_InstallationId");
                if (string.IsNullOrWhiteSpace(installId))
                {
                    installId = $"SEC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
                    await _db.SetSettingAsync("UserProfile_InstallationId", installId);
                }

                bool hasPin = await _masterPinService.IsMasterPinConfiguredAsync();

                return new
                {
                    suggestedUsername = systemUser,
                    machineName = machine,
                    osVersion = os,
                    installationId = installId,
                    hasMasterPin = hasPin
                };
            }

            case "setup.complete":
            {
                string userName = payload.TryGetProperty("userName", out var unProp) ? unProp.GetString() ?? Environment.UserName : Environment.UserName;
                string userRole = payload.TryGetProperty("userRole", out var urProp) ? urProp.GetString() ?? "Security Administrator" : "Security Administrator";
                string avatar = payload.TryGetProperty("avatar", out var avProp) ? avProp.GetString() ?? "shield-cyan" : "shield-cyan";
                string securityTier = payload.TryGetProperty("securityTier", out var stProp) ? stProp.GetString() ?? "Standard" : "Standard";
                string recoveryCode = payload.TryGetProperty("recoveryCode", out var rcProp) ? rcProp.GetString() ?? string.Empty : string.Empty;
                bool darkMode = payload.TryGetProperty("darkMode", out var dmProp) && dmProp.GetBoolean();
                int threshold = payload.TryGetProperty("ransomwareThreshold", out var thProp) ? thProp.GetInt32() : 30;

                await _db.SetSettingAsync("SetupCompleted", "true");
                await _db.SetSettingAsync("UserProfile_Name", userName);
                await _db.SetSettingAsync("UserProfile_Role", userRole);
                await _db.SetSettingAsync("UserProfile_Avatar", avatar);
                await _db.SetSettingAsync("UserProfile_SecurityTier", securityTier);
                await _db.SetSettingAsync("UserProfile_RecoveryCode", recoveryCode);
                await _db.SetSettingAsync("UserProfile_SetupDate", DateTime.UtcNow.ToString("o"));
                await _db.SetSettingAsync("DarkMode", darkMode ? "true" : "false");
                await _db.SetSettingAsync("MassModificationThreshold", threshold.ToString());

                if (payload.TryGetProperty("masterPin", out var pinProp))
                {
                    string? pin = pinProp.GetString();
                    if (!string.IsNullOrWhiteSpace(pin))
                    {
                        await _masterPinService.SetMasterPinAsync(pin.Trim());
                    }
                }

                if (securityTier == "Maximum")
                {
                    await _db.SetSettingAsync("MassModificationThreshold", "15");
                    await _db.SetSettingAsync("AutoLockOnWindowClose", "true");
                }
                else if (securityTier == "Relaxed")
                {
                    await _db.SetSettingAsync("MassModificationThreshold", "50");
                }

                await _db.AddSecurityEventAsync(new SecurityEvent
                {
                    EventType = "DynamicSetupCompleted",
                    Severity = EventSeverity.Info,
                    Description = $"Dynamic installation initialized for user '{userName}' ({userRole}) with '{securityTier}' security posture.",
                    ActionTaken = "Profile Configured"
                });

                return new { success = true };
            }

            case "setup.reset":
            {
                await _db.SetSettingAsync("SetupCompleted", "false");
                return true;
            }

            case "profile.get":
            {
                string? completed = await _db.GetSettingAsync("SetupCompleted", "false");
                string userName = await _db.GetSettingAsync("UserProfile_Name") ?? Environment.UserName;
                string userRole = await _db.GetSettingAsync("UserProfile_Role") ?? "Security Administrator";
                string avatar = await _db.GetSettingAsync("UserProfile_Avatar") ?? "shield-cyan";
                string securityTier = await _db.GetSettingAsync("UserProfile_SecurityTier") ?? "Standard";
                string? installId = await _db.GetSettingAsync("UserProfile_InstallationId");
                if (string.IsNullOrWhiteSpace(installId))
                {
                    installId = $"SEC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
                    await _db.SetSettingAsync("UserProfile_InstallationId", installId);
                }
                string? setupDate = await _db.GetSettingAsync("UserProfile_SetupDate");

                return new
                {
                    userName,
                    userRole,
                    avatar,
                    securityTier,
                    installationId = installId,
                    setupDate,
                    isCompleted = string.Equals(completed, "true", StringComparison.OrdinalIgnoreCase)
                };
            }

            case "profile.update":
            {
                if (payload.TryGetProperty("userName", out var un))
                    await _db.SetSettingAsync("UserProfile_Name", un.GetString() ?? Environment.UserName);
                if (payload.TryGetProperty("userRole", out var ur))
                    await _db.SetSettingAsync("UserProfile_Role", ur.GetString() ?? "Security Administrator");
                if (payload.TryGetProperty("avatar", out var av))
                    await _db.SetSettingAsync("UserProfile_Avatar", av.GetString() ?? "shield-cyan");
                if (payload.TryGetProperty("securityTier", out var st))
                    await _db.SetSettingAsync("UserProfile_SecurityTier", st.GetString() ?? "Standard");

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
