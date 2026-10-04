using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;
using SecApper.FolderLocker.Services;
using SecApper.FolderLocker.Views;
using SecApper.Security.Data;
using SecApper.Security.Models;
using SecApper.Security.Ransomware;
using SecApper.Security.Recovery;
using SecApper.Security.Security;
using SecApper.Security.Services;
using SecApper.Security.Updates;

namespace SecApper.FolderLocker.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IDatabaseService _databaseService;
    private readonly IFolderLockService _folderLockService;
    private readonly IRecoveryService _recoveryService;
    private readonly IRansomwareProtectionService _ransomwareService;
    private readonly IFolderPathValidator _pathValidator;
    private readonly IPasswordService _passwordService;
    private readonly IExplorerIntegrationService _explorerService;
    private readonly SystemTrayService _trayService;
    private readonly IUpdateService _updateService;

    private string _activeTab = "Dashboard";
    private bool _isBusy;
    private string _statusMessage = "SecApper Folder Locker Ready";
    private ThreatAlertViewModel? _activeAlert;
    private bool _explorerIntegrationEnabled;

    private bool _autoCheckUpdates = true;
    private string _updateCheckFrequency = "Daily";
    private string _updateStatusMessage = "SecApper is up to date.";
    private string _lastUpdateCheckText = "Never";
    private bool _isCheckingForUpdates;

    public ObservableCollection<FolderItemViewModel> Folders { get; } = new();
    public ObservableCollection<SecurityEvent> SecurityEvents { get; } = new();
    public ObservableCollection<SecurityEventItemViewModel> AllEvents { get; } = new();
    public ObservableCollection<SecurityEventItemViewModel> FilteredEvents { get; } = new();
    public ObservableCollection<RecoveryIssue> RecoveryIssues { get; } = new();

    public string CurrentAppVersion => _updateService.CurrentVersion;

    public string ActiveTab
    {
        get => _activeTab;
        set
        {
            if (SetProperty(ref _activeTab, value))
            {
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(PageSubtitle));
            }
        }
    }

    public string PageTitle => ActiveTab switch
    {
        "Dashboard" => "Dashboard",
        "FolderLocker" => "Folder Locker",
        "ProtectedFolders" => "Protected Folders",
        "RansomwareProtection" => "Ransomware Protection",
        "SecurityEvents" => "Security Events",
        "Recovery" => "Recovery Center",
        "Updates" => "Software Updates",
        "Settings" => "Settings",
        _ => "SecApper"
    };

    public string PageSubtitle => ActiveTab switch
    {
        "Dashboard" => "Monitor and manage your device security.",
        "FolderLocker" => "Protect your folders with secure Windows permissions.",
        "ProtectedFolders" => "Manage folders currently protected by SecApper.",
        "RansomwareProtection" => "Monitor protected folders for suspicious file activity.",
        "SecurityEvents" => "Review security activity detected by SecApper.",
        "Recovery" => "Recover folder access after interrupted security operations.",
        "Updates" => "Keep SecApper secure with the latest version.",
        "Settings" => "Configure application security, privacy, and system preferences.",
        _ => "Secure Your World"
    };

    public string SystemSecurityStatusText => (HasActiveAlert || HasRecoveryIssues)
        ? "Attention Needed"
        : "System Protected";

    public System.Windows.Media.Brush SystemSecurityStatusBrush => (HasActiveAlert || HasRecoveryIssues)
        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(197, 32, 43))
        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 163, 74));

    public System.Windows.Media.Brush SystemSecurityStatusBadgeBackground => (HasActiveAlert || HasRecoveryIssues)
        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 226, 226))
        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 252, 231));

    public string ThreatLevelText => HasActiveAlert
        ? "HIGH"
        : (SecurityEvents.Any(e => e.Severity == EventSeverity.Warning) ? "MEDIUM" : "LOW");

    public System.Windows.Media.Brush ThreatLevelBadgeColor => ThreatLevelText switch
    {
        "HIGH" or "CRITICAL" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(197, 32, 43)),
        "MEDIUM" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(217, 119, 6)),
        _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 163, 74))
    };

    public int SuspiciousEventsCount => SecurityEvents.Count(e => e.Severity == EventSeverity.Warning || e.Severity == EventSeverity.High || e.Severity == EventSeverity.Critical);

    public string TotalLockedStorageText => $"{LockedCount} Folders Locked";

    public string RecoveryStatusText => HasRecoveryIssues ? "RECOVERY REQUIRED" : "SYSTEM HEALTHY";
    public string LastRecoveryScanText { get; private set; } = "Never";

    // Folder Locker interactive selection properties
    private string? _selectedLockerPath;
    private string? _selectedLockerFolderName;
    private FolderItemViewModel? _selectedLockerItem;

    public string? SelectedLockerPath
    {
        get => _selectedLockerPath;
        set => SetProperty(ref _selectedLockerPath, value);
    }

    public string? SelectedLockerFolderName
    {
        get => _selectedLockerFolderName;
        set => SetProperty(ref _selectedLockerFolderName, value);
    }

    public FolderItemViewModel? SelectedLockerItem
    {
        get => _selectedLockerItem;
        set
        {
            if (SetProperty(ref _selectedLockerItem, value))
            {
                OnPropertyChanged(nameof(HasSelectedLocker));
                OnPropertyChanged(nameof(SelectedLockerStatusText));
                OnPropertyChanged(nameof(IsSelectedLockerLocked));
            }
        }
    }

    public bool HasSelectedLocker => !string.IsNullOrEmpty(_selectedLockerPath);

    public string SelectedLockerStatusText => _selectedLockerItem != null
        ? _selectedLockerItem.StatusText
        : (HasSelectedLocker ? "READY TO LOCK" : "NO FOLDER SELECTED");

    public bool IsSelectedLockerLocked => _selectedLockerItem?.IsLocked == true;

    // Security Events filtering
    private string _eventFilter = "ALL";
    public string EventFilter
    {
        get => _eventFilter;
        set
        {
            if (SetProperty(ref _eventFilter, value))
            {
                ApplyEventFilter();
            }
        }
    }

    private string _eventSearchText = string.Empty;
    public string EventSearchText
    {
        get => _eventSearchText;
        set
        {
            if (SetProperty(ref _eventSearchText, value))
            {
                ApplyEventFilter();
            }
        }
    }

    private SecurityEventItemViewModel? _selectedEvent;
    public SecurityEventItemViewModel? SelectedEvent
    {
        get => _selectedEvent;
        set => SetProperty(ref _selectedEvent, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ThreatAlertViewModel? ActiveAlert
    {
        get => _activeAlert;
        set
        {
            if (SetProperty(ref _activeAlert, value))
            {
                OnPropertyChanged(nameof(HasActiveAlert));
                UpdateSecurityStatus();
            }
        }
    }

    public bool HasActiveAlert => _activeAlert != null;

    public bool ExplorerIntegrationEnabled
    {
        get => _explorerIntegrationEnabled;
        set
        {
            if (SetProperty(ref _explorerIntegrationEnabled, value))
            {
                if (value) _explorerService.EnableContextMenu();
                else _explorerService.DisableContextMenu();
            }
        }
    }

    public bool AutoCheckUpdates
    {
        get => _autoCheckUpdates;
        set
        {
            if (SetProperty(ref _autoCheckUpdates, value))
            {
                _ = _databaseService.SetSettingAsync("AutoCheckUpdates", value.ToString().ToLowerInvariant());
            }
        }
    }

    public string UpdateCheckFrequency
    {
        get => _updateCheckFrequency;
        set
        {
            if (SetProperty(ref _updateCheckFrequency, value))
            {
                _ = _databaseService.SetSettingAsync("UpdateCheckFrequency", value);
            }
        }
    }

    public string UpdateStatusMessage
    {
        get => _updateStatusMessage;
        set => SetProperty(ref _updateStatusMessage, value);
    }

    public string LastUpdateCheckText
    {
        get => _lastUpdateCheckText;
        set => SetProperty(ref _lastUpdateCheckText, value);
    }

    public bool IsCheckingForUpdates
    {
        get => _isCheckingForUpdates;
        set => SetProperty(ref _isCheckingForUpdates, value);
    }

    public int TotalFoldersCount => Folders.Count;
    public int LockedCount => Folders.Count(f => f.Status == FolderStatus.Locked);
    public int UnlockedCount => Folders.Count(f => f.Status == FolderStatus.Unlocked);
    public bool HasFolders => Folders.Count > 0;
    public bool HasRecoveryIssues => RecoveryIssues.Count > 0;

    private string _updateManifestUrl = "https://raw.githubusercontent.com/adilmahboobalam/Secappers/main/latest.json";
    public string UpdateManifestUrl
    {
        get => _updateManifestUrl;
        set
        {
            if (_updateManifestUrl != value)
            {
                _updateManifestUrl = value;
                OnPropertyChanged();
                string normalized = UpdateService.NormalizeManifestUrl(value);
                _ = _databaseService.SetSettingAsync("UpdateManifestUrl", normalized);
            }
        }
    }

    public ICommand SelectTabCommand { get; }
    public ICommand AddFolderCommand { get; }
    public ICommand LockFolderCommand { get; }
    public ICommand UnlockFolderCommand { get; }
    public ICommand LockAllCommand { get; }
    public ICommand PanicLockCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ScanRecoveryCommand { get; }
    public ICommand ClearEventsCommand { get; }
    public ICommand ChangePasswordCommand { get; }
    public ICommand RemoveFolderCommand { get; }
    public ICommand OpenInExplorerCommand { get; }
    public ICommand ViewDetailsCommand { get; }
    public ICommand DismissAlertCommand { get; }
    public ICommand RepairProtectionCommand { get; }
    public ICommand RestoreAccessCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }
    public ICommand SimulateUpdateCommand { get; }
    public ICommand SelectFolderForLockerCommand { get; }
    public ICommand LockSelectedLockerCommand { get; }
    public ICommand UnlockSelectedLockerCommand { get; }
    public ICommand SetEventFilterCommand { get; }
    public ICommand ExportEventsCommand { get; }
    public ICommand SimulateThreatAlertCommand { get; }

    public MainViewModel(
        IDatabaseService databaseService,
        IFolderLockService folderLockService,
        IRecoveryService recoveryService,
        IRansomwareProtectionService ransomwareService,
        IFolderPathValidator pathValidator,
        IPasswordService passwordService,
        IExplorerIntegrationService explorerService,
        SystemTrayService trayService,
        IUpdateService updateService)
    {
        _databaseService = databaseService;
        _folderLockService = folderLockService;
        _recoveryService = recoveryService;
        _ransomwareService = ransomwareService;
        _pathValidator = pathValidator;
        _passwordService = passwordService;
        _explorerService = explorerService;
        _trayService = trayService;
        _updateService = updateService;

        _explorerIntegrationEnabled = _explorerService.IsContextMenuEnabled();

        // Wire tray events
        _trayService.LockAllRequested += async () => await ExecuteLockAllAsync();
        _trayService.PanicLockRequested += async () => await ExecutePanicLockAsync();

        // Wire ransomware alert
        _ransomwareService.ThreatDetected += OnThreatDetected;

        // Initialize Commands
        SelectTabCommand = new RelayCommand(tab => ActiveTab = tab?.ToString() ?? "Folders");
        AddFolderCommand = new AsyncRelayCommand(ExecuteAddFolderAsync);
        LockFolderCommand = new AsyncRelayCommand(param => ExecuteLockFolderAsync(param as FolderItemViewModel));
        UnlockFolderCommand = new AsyncRelayCommand(param => ExecuteUnlockFolderAsync(param as FolderItemViewModel));
        LockAllCommand = new AsyncRelayCommand(ExecuteLockAllAsync);
        PanicLockCommand = new AsyncRelayCommand(ExecutePanicLockAsync);
        RefreshCommand = new AsyncRelayCommand(ExecuteRefreshAsync);
        ScanRecoveryCommand = new AsyncRelayCommand(ExecuteScanRecoveryAsync);
        ClearEventsCommand = new AsyncRelayCommand(ExecuteClearEventsAsync);
        ChangePasswordCommand = new AsyncRelayCommand(param => ExecuteChangePasswordAsync(param as FolderItemViewModel));
        RemoveFolderCommand = new AsyncRelayCommand(param => ExecuteRemoveFolderAsync(param as FolderItemViewModel));
        OpenInExplorerCommand = new RelayCommand(param => (param as FolderItemViewModel)?.OpenInExplorer());
        ViewDetailsCommand = new RelayCommand(param => ExecuteViewDetails(param as FolderItemViewModel));
        DismissAlertCommand = new RelayCommand(() => ActiveAlert = null);
        RepairProtectionCommand = new AsyncRelayCommand(param => ExecuteRepairProtectionAsync(param as RecoveryIssue));
        RestoreAccessCommand = new AsyncRelayCommand(param => ExecuteRestoreAccessAsync(param as RecoveryIssue));
        CheckForUpdatesCommand = new AsyncRelayCommand(() => ExecuteCheckForUpdatesAsync(isManual: true));
        SimulateUpdateCommand = new AsyncRelayCommand(ExecuteSimulateUpdateAsync);
        SelectFolderForLockerCommand = new AsyncRelayCommand(ExecuteSelectFolderForLockerAsync);
        LockSelectedLockerCommand = new AsyncRelayCommand(ExecuteLockSelectedLockerAsync);
        UnlockSelectedLockerCommand = new AsyncRelayCommand(ExecuteUnlockSelectedLockerAsync);
        SetEventFilterCommand = new RelayCommand(f => EventFilter = f?.ToString() ?? "ALL");
        ExportEventsCommand = new RelayCommand(ExecuteExportEvents);
        SimulateThreatAlertCommand = new AsyncRelayCommand(ExecuteSimulateThreatAlertAsync);
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        StatusMessage = "Initializing SecApper offline database...";
        try
        {
            await _databaseService.InitializeAsync();
            await LoadUpdateSettingsAsync();
            await LoadFoldersAsync();
            await LoadSecurityEventsAsync();
            await ExecuteScanRecoveryAsync();
            StatusMessage = "Ready";

            _ = Task.Run(async () =>
            {
                await Task.Delay(2500);
                await ExecuteCheckForUpdatesAsync(isManual: false);
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Initialization error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadFoldersAsync()
    {
        var records = await _databaseService.GetAllFoldersAsync();
        Folders.Clear();

        foreach (var rec in records)
        {
            var item = new FolderItemViewModel(rec);
            Folders.Add(item);

            // Start ransomware monitoring if protected mode
            if (rec.ProtectionMode == ProtectionMode.Protected || rec.ProtectionMode == ProtectionMode.LockedAndProtected)
            {
                var policy = await _databaseService.GetPolicyForFolderAsync(rec.Id);
                _ransomwareService.StartMonitoringFolder(rec, policy);
            }
        }

        UpdateCounts();
    }

    private async Task LoadSecurityEventsAsync()
    {
        var events = await _databaseService.GetRecentSecurityEventsAsync(200);
        SecurityEvents.Clear();
        AllEvents.Clear();

        var folderDict = Folders.ToDictionary(f => f.Id, f => f.FolderName);

        foreach (var evt in events)
        {
            SecurityEvents.Add(evt);
            string folderDisplay = "General / Device";
            if (!string.IsNullOrEmpty(evt.FolderId) && folderDict.TryGetValue(evt.FolderId, out var name))
            {
                folderDisplay = name;
            }
            AllEvents.Add(new SecurityEventItemViewModel(evt, folderDisplay));
        }

        ApplyEventFilter();
        UpdateSecurityStatus();
    }

    private void ApplyEventFilter()
    {
        FilteredEvents.Clear();
        var query = AllEvents.AsEnumerable();

        if (!string.Equals(EventFilter, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(e => string.Equals(e.SeverityText, EventFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(EventSearchText))
        {
            string term = EventSearchText.Trim();
            query = query.Where(e =>
                (e.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.ProcessName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.EventType?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.FolderDisplay?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        foreach (var item in query)
        {
            FilteredEvents.Add(item);
        }
    }

    private void UpdateCounts()
    {
        OnPropertyChanged(nameof(TotalFoldersCount));
        OnPropertyChanged(nameof(LockedCount));
        OnPropertyChanged(nameof(UnlockedCount));
        OnPropertyChanged(nameof(HasFolders));
        UpdateSecurityStatus();
    }

    private void UpdateSecurityStatus()
    {
        OnPropertyChanged(nameof(SystemSecurityStatusText));
        OnPropertyChanged(nameof(SystemSecurityStatusBrush));
        OnPropertyChanged(nameof(SystemSecurityStatusBadgeBackground));
        OnPropertyChanged(nameof(ThreatLevelText));
        OnPropertyChanged(nameof(ThreatLevelBadgeColor));
        OnPropertyChanged(nameof(SuspiciousEventsCount));
        OnPropertyChanged(nameof(TotalLockedStorageText));
        OnPropertyChanged(nameof(RecoveryStatusText));
    }

    private async Task ExecuteAddFolderAsync()
    {
        // 1. Native folder picker
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a folder to lock with SecApper Folder Locker",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        string selectedPath = dialog.SelectedPath;

        // 2. Validate folder path
        var existingPaths = Folders.Select(f => f.FolderPath);
        var validation = _pathValidator.ValidatePath(selectedPath, existingPaths);
        if (!validation.IsValid)
        {
            MessageBox.Show(validation.ErrorMessage, "Invalid Folder Path", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 3. Open Password Creation Dialog
        var lockVm = new LockFolderDialogViewModel
        {
            FolderPath = selectedPath
        };

        var dialogWindow = new LockFolderDialog
        {
            DataContext = lockVm,
            Owner = Application.Current.MainWindow
        };

        if (dialogWindow.ShowDialog() != true)
            return;

        // 4. Create and lock folder
        IsBusy = true;
        StatusMessage = $"Securing {lockVm.FolderName}...";

        try
        {
            var hashResult = _passwordService.HashPassword(lockVm.Password);
            var folder = new FolderRecord
            {
                FolderPath = lockVm.FolderPath,
                FolderName = lockVm.FolderName,
                Status = FolderStatus.Unlocked,
                PasswordHash = hashResult.Hash,
                PasswordSalt = hashResult.Salt,
                PasswordAlgorithm = hashResult.Algorithm,
                PasswordIterations = hashResult.Iterations,
                ProtectionMode = ProtectionMode.LockedAndProtected
            };

            await _databaseService.AddFolderAsync(folder);

            // Add policy
            await _databaseService.UpsertPolicyAsync(new ProtectionPolicy
            {
                FolderId = folder.Id,
                ProtectionEnabled = true,
                MassModificationThreshold = 25
            });

            // Perform lock
            var lockResult = await _folderLockService.LockFolderAsync(folder.Id);
            if (!lockResult.Success)
            {
                MessageBox.Show($"Lock failed: {lockResult.ErrorMessage}", "Lock Error", MessageBoxButton.OK, MessageBoxImage.Error);
                await _databaseService.DeleteFolderAsync(folder.Id);
                return;
            }

            // Start ransomware monitoring
            var policy = await _databaseService.GetPolicyForFolderAsync(folder.Id);
            _ransomwareService.StartMonitoringFolder(folder, policy);

            await LoadFoldersAsync();
            await LoadSecurityEventsAsync();

            _trayService.ShowNotification("Folder Locked", $"{folder.FolderName} is now secured offline.");
            StatusMessage = $"Folder '{folder.FolderName}' locked successfully.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unexpected error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteUnlockFolderAsync(FolderItemViewModel? item)
    {
        if (item == null) return;

        var unlockVm = new UnlockDialogViewModel
        {
            FolderName = item.FolderName,
            FolderPath = item.FolderPath
        };

        var dialog = new UnlockDialog
        {
            DataContext = unlockVm,
            Owner = Application.Current.MainWindow
        };

        dialog.ValidatePasswordAsync = async pwd =>
        {
            var folder = await _databaseService.GetFolderByIdAsync(item.Id);
            if (folder == null) return "Folder record not found.";
            if (!_passwordService.VerifyPassword(pwd, folder.PasswordHash, folder.PasswordSalt, folder.PasswordAlgorithm, folder.PasswordIterations))
            {
                return "Incorrect password.";
            }
            return null;
        };

        if (dialog.ShowDialog() != true)
            return;

        IsBusy = true;
        StatusMessage = $"Unlocking {item.FolderName}...";

        try
        {
            var result = await _folderLockService.UnlockFolderAsync(item.Id, unlockVm.Password);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage, "Unlock Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            item.NotifyStateChanged();
            await LoadFoldersAsync();
            await LoadSecurityEventsAsync();

            _trayService.ShowNotification("Folder Unlocked", $"{item.FolderName} has been unlocked.");
            StatusMessage = $"Folder '{item.FolderName}' unlocked.";

            // Open folder in Explorer as requested in specification
            item.OpenInExplorer();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error unlocking folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> PromptUnlockForFolderAsync(FolderRecord folder)
    {
        var unlockVm = new UnlockDialogViewModel
        {
            FolderName = folder.FolderName,
            FolderPath = folder.FolderPath
        };

        var dialog = new UnlockDialog
        {
            DataContext = unlockVm,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true
        };

        dialog.ValidatePasswordAsync = async pwd =>
        {
            await Task.Yield();
            if (!_passwordService.VerifyPassword(pwd, folder.PasswordHash, folder.PasswordSalt, folder.PasswordAlgorithm, folder.PasswordIterations))
            {
                return "Incorrect password.";
            }
            return null;
        };

        if (dialog.ShowDialog() != true)
            return false;

        IsBusy = true;
        StatusMessage = $"Unlocking {folder.FolderName}...";

        try
        {
            var result = await _folderLockService.UnlockFolderAsync(folder.Id, unlockVm.Password);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage, "Unlock Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            await LoadFoldersAsync();
            await LoadSecurityEventsAsync();

            _trayService.ShowNotification("Folder Unlocked", $"{folder.FolderName} has been unlocked.");
            StatusMessage = $"Folder '{folder.FolderName}' unlocked.";

            // Open folder in Explorer
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", folder.FolderPath) { UseShellExecute = true });
            }
            catch
            {
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error unlocking folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteLockFolderAsync(FolderItemViewModel? item)
    {
        if (item == null) return;

        IsBusy = true;
        StatusMessage = $"Locking {item.FolderName}...";

        try
        {
            var result = await _folderLockService.LockFolderAsync(item.Id);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage, "Lock Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            item.NotifyStateChanged();
            await LoadFoldersAsync();
            await LoadSecurityEventsAsync();

            _trayService.ShowNotification("Folder Locked", $"{item.FolderName} is now protected.");
            StatusMessage = $"Folder '{item.FolderName}' locked successfully.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error locking folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteLockAllAsync()
    {
        IsBusy = true;
        StatusMessage = "Locking all unlocked folders...";
        try
        {
            int locked = await _ransomwareService.LockAllFoldersAsync();
            await LoadFoldersAsync();
            await LoadSecurityEventsAsync();
            _trayService.ShowNotification("Lock All Complete", $"{locked} folder(s) secured.");
            StatusMessage = $"Locked {locked} folder(s).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecutePanicLockAsync()
    {
        IsBusy = true;
        StatusMessage = "🚨 EXECUTING PANIC LOCK...";
        try
        {
            int count = await _ransomwareService.PanicLockAllAsync();
            await LoadFoldersAsync();
            await LoadSecurityEventsAsync();
            _trayService.ShowNotification("🚨 PANIC LOCK ACTIVATED", $"All {count} registered folder(s) have been locked immediately.", System.Windows.Forms.ToolTipIcon.Warning);
            StatusMessage = $"Panic Lock executed. {count} folder(s) locked.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteChangePasswordAsync(FolderItemViewModel? item)
    {
        if (item == null) return;

        var changeVm = new ChangePasswordDialogViewModel
        {
            FolderName = item.FolderName
        };

        var dialog = new ChangePasswordDialog
        {
            DataContext = changeVm,
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true) return;

        // Verify current password
        bool currentValid = _passwordService.VerifyPassword(
            changeVm.CurrentPassword,
            item.Model.PasswordHash,
            item.Model.PasswordSalt,
            item.Model.PasswordAlgorithm,
            item.Model.PasswordIterations);

        if (!currentValid)
        {
            MessageBox.Show("The current password entered is incorrect.", "Password Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Validate new password strength
        var strength = _passwordService.ValidatePasswordStrength(changeVm.NewPassword);
        if (!strength.IsValid)
        {
            MessageBox.Show(strength.ErrorMessage, "Password Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (changeVm.NewPassword != changeVm.ConfirmNewPassword)
        {
            MessageBox.Show("New password confirmation does not match.", "Password Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Hash new password and update DB
        var newHash = _passwordService.HashPassword(changeVm.NewPassword);
        item.Model.PasswordHash = newHash.Hash;
        item.Model.PasswordSalt = newHash.Salt;
        item.Model.PasswordAlgorithm = newHash.Algorithm;
        item.Model.PasswordIterations = newHash.Iterations;

        await _databaseService.UpdateFolderAsync(item.Model);
        await _databaseService.AddSecurityEventAsync(new SecurityEvent
        {
            FolderId = item.Id,
            EventType = "PasswordChanged",
            Severity = EventSeverity.Info,
            Description = $"Password updated for folder '{item.FolderName}'.",
            ActionTaken = "Updated cryptographic hash"
        });

        MessageBox.Show($"Password successfully updated for '{item.FolderName}'.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task ExecuteRemoveFolderAsync(FolderItemViewModel? item)
    {
        if (item == null) return;

        // Confirm
        var confirm = MessageBox.Show(
            $"Are you sure you want to remove '{item.FolderName}' from SecApper Folder Locker?\n\nNOTE: This will restore normal folder access and will NOT delete your files on disk.",
            "Confirm Folder Removal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        // If folder is currently locked, ask for password to unlock it first before removal
        if (item.Status == FolderStatus.Locked)
        {
            var unlockVm = new UnlockDialogViewModel
            {
                FolderName = item.FolderName,
                FolderPath = item.FolderPath
            };

            var dialog = new UnlockDialog
            {
                DataContext = unlockVm,
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true) return;

            var unlockResult = await _folderLockService.UnlockFolderAsync(item.Id, unlockVm.Password);
            if (!unlockResult.Success)
            {
                MessageBox.Show(unlockResult.ErrorMessage, "Unlock Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        _ransomwareService.StopMonitoringFolder(item.Id);
        await _databaseService.DeleteFolderAsync(item.Id);

        await _databaseService.AddSecurityEventAsync(new SecurityEvent
        {
            FolderId = item.Id,
            EventType = "FolderRemoved",
            Severity = EventSeverity.Info,
            Description = $"Folder '{item.FolderName}' was safely removed from locker. Files remain intact.",
            ActionTaken = "Record removed from database"
        });

        await LoadFoldersAsync();
        await LoadSecurityEventsAsync();
        StatusMessage = $"Folder '{item.FolderName}' was removed from SecApper.";
    }

    private void ExecuteViewDetails(FolderItemViewModel? item)
    {
        if (item == null) return;
        var dialog = new FolderDetailsDialog(item)
        {
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    private async Task ExecuteRefreshAsync()
    {
        IsBusy = true;
        StatusMessage = "Refreshing...";
        try
        {
            await LoadFoldersAsync();
            await LoadSecurityEventsAsync();
            await ExecuteScanRecoveryAsync();
            StatusMessage = "Refreshed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteScanRecoveryAsync()
    {
        var issues = await _recoveryService.ScanForInconsistenciesAsync();
        RecoveryIssues.Clear();
        foreach (var issue in issues)
        {
            RecoveryIssues.Add(issue);
        }
        LastRecoveryScanText = DateTime.Now.ToString("MMM dd, yyyy HH:mm");
        OnPropertyChanged(nameof(HasRecoveryIssues));
        OnPropertyChanged(nameof(LastRecoveryScanText));
        OnPropertyChanged(nameof(RecoveryStatusText));
        UpdateSecurityStatus();
    }

    private async Task ExecuteRepairProtectionAsync(RecoveryIssue? issue)
    {
        if (issue == null) return;
        IsBusy = true;
        StatusMessage = $"Repairing protection for {issue.Folder.FolderName}...";
        try
        {
            bool ok = await _recoveryService.RepairProtectionAsync(issue.Folder.Id);
            if (ok)
            {
                await LoadFoldersAsync();
                await ExecuteScanRecoveryAsync();
                StatusMessage = "Protection successfully repaired.";
            }
            else
            {
                MessageBox.Show("Could not repair protection. Administrator privileges may be required.", "Repair Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteRestoreAccessAsync(RecoveryIssue? issue)
    {
        if (issue == null) return;
        IsBusy = true;
        StatusMessage = $"Restoring access for {issue.Folder.FolderName}...";
        try
        {
            bool ok = await _recoveryService.RestoreAccessAsync(issue.Folder.Id);
            if (ok)
            {
                await LoadFoldersAsync();
                await ExecuteScanRecoveryAsync();
                StatusMessage = "Access successfully restored.";
            }
            else
            {
                MessageBox.Show("Could not restore access. Please ensure backup exists or run as Administrator.", "Restore Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteClearEventsAsync()
    {
        await _databaseService.ClearEventsAsync();
        SecurityEvents.Clear();
        AllEvents.Clear();
        FilteredEvents.Clear();
        UpdateSecurityStatus();
        StatusMessage = "Security event log cleared.";
    }

    private async Task ExecuteSelectFolderForLockerAsync()
    {
        await Task.Yield();
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a folder to protect with SecApper Folder Locker",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        string path = dialog.SelectedPath;
        var existing = Folders.FirstOrDefault(f => string.Equals(f.FolderPath, path, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            var existingPaths = Folders.Select(f => f.FolderPath);
            var validation = _pathValidator.ValidatePath(path, existingPaths);
            if (!validation.IsValid)
            {
                MessageBox.Show(validation.ErrorMessage, "Invalid Folder Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        SelectedLockerPath = path;
        string name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
        SelectedLockerFolderName = string.IsNullOrWhiteSpace(name) ? path : name;
        SelectedLockerItem = existing;

        StatusMessage = existing != null
            ? $"Selected registered folder: {SelectedLockerFolderName} ({existing.StatusText})"
            : $"Selected new folder: {SelectedLockerFolderName}. Ready to lock.";
    }

    private async Task ExecuteLockSelectedLockerAsync()
    {
        if (string.IsNullOrEmpty(SelectedLockerPath))
        {
            await ExecuteSelectFolderForLockerAsync();
            if (string.IsNullOrEmpty(SelectedLockerPath)) return;
        }

        if (SelectedLockerItem != null)
        {
            await ExecuteLockFolderAsync(SelectedLockerItem);
            OnPropertyChanged(nameof(SelectedLockerStatusText));
            OnPropertyChanged(nameof(IsSelectedLockerLocked));
            return;
        }

        // New folder: open password creation dialog
        var lockVm = new LockFolderDialogViewModel
        {
            FolderPath = SelectedLockerPath
        };

        var dialogWindow = new LockFolderDialog
        {
            DataContext = lockVm,
            Owner = Application.Current.MainWindow
        };

        if (dialogWindow.ShowDialog() != true)
            return;

        IsBusy = true;
        StatusMessage = $"Securing {lockVm.FolderName}...";

        try
        {
            var hashResult = _passwordService.HashPassword(lockVm.Password);
            var folder = new FolderRecord
            {
                FolderPath = lockVm.FolderPath,
                FolderName = lockVm.FolderName,
                Status = FolderStatus.Unlocked,
                PasswordHash = hashResult.Hash,
                PasswordSalt = hashResult.Salt,
                PasswordAlgorithm = hashResult.Algorithm,
                PasswordIterations = hashResult.Iterations,
                ProtectionMode = ProtectionMode.LockedAndProtected
            };

            await _databaseService.AddFolderAsync(folder);
            await _databaseService.UpsertPolicyAsync(new ProtectionPolicy
            {
                FolderId = folder.Id,
                ProtectionEnabled = true,
                MassModificationThreshold = 25
            });

            var lockResult = await _folderLockService.LockFolderAsync(folder.Id);
            if (!lockResult.Success)
            {
                MessageBox.Show($"Lock failed: {lockResult.ErrorMessage}", "Lock Error", MessageBoxButton.OK, MessageBoxImage.Error);
                await _databaseService.DeleteFolderAsync(folder.Id);
                return;
            }

            var policy = await _databaseService.GetPolicyForFolderAsync(folder.Id);
            _ransomwareService.StartMonitoringFolder(folder, policy);

            await LoadFoldersAsync();
            await LoadSecurityEventsAsync();

            SelectedLockerItem = Folders.FirstOrDefault(f => f.Id == folder.Id);
            OnPropertyChanged(nameof(SelectedLockerStatusText));
            OnPropertyChanged(nameof(IsSelectedLockerLocked));

            _trayService.ShowNotification("Folder Locked", $"{folder.FolderName} is now secured offline.");
            StatusMessage = $"Folder '{folder.FolderName}' locked successfully.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unexpected error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteUnlockSelectedLockerAsync()
    {
        if (SelectedLockerItem == null) return;
        await ExecuteUnlockFolderAsync(SelectedLockerItem);
        OnPropertyChanged(nameof(SelectedLockerStatusText));
        OnPropertyChanged(nameof(IsSelectedLockerLocked));
    }

    private void ExecuteExportEvents()
    {
        try
        {
            using var saveDialog = new System.Windows.Forms.SaveFileDialog
            {
                Title = "Export SecApper Security Logs",
                Filter = "CSV File (*.csv)|*.csv|Log File (*.log)|*.log|Text File (*.txt)|*.txt",
                FileName = $"SecApper_Security_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Timestamp,Severity,Event,Folder,Process,ActionTaken,Description");

            foreach (var evt in AllEvents)
            {
                sb.AppendLine($"\"{evt.FullTimeText}\",\"{evt.SeverityText}\",\"{evt.EventType}\",\"{evt.FolderDisplay}\",\"{evt.ProcessName}\",\"{evt.ActionTaken}\",\"{evt.Description.Replace("\"", "\"\"")}\"");
            }

            File.WriteAllText(saveDialog.FileName, sb.ToString());
            MessageBox.Show($"Successfully exported {AllEvents.Count} event(s) to:\n{saveDialog.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export logs: {ex.Message}", "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExecuteSimulateThreatAlertAsync()
    {
        IsBusy = true;
        StatusMessage = "Simulating ransomware threat activity...";
        try
        {
            var testEvent = new SecurityEvent
            {
                EventType = "SuspiciousMassModification",
                Severity = EventSeverity.Warning,
                ProcessName = "secapper-heuristic-test.exe",
                ProcessPath = "C:\\Windows\\System32\\secapper-test.exe",
                Description = "Simulated suspicious mass-file modification pattern detected in protected folder.",
                ActionTaken = "Heuristic threshold triggered; alert logged to security database"
            };

            await _databaseService.AddSecurityEventAsync(testEvent);
            await LoadSecurityEventsAsync();

            _trayService.ShowNotification("Security Alert Logged", "Simulated suspicious activity test completed.", System.Windows.Forms.ToolTipIcon.Warning);
            StatusMessage = "Simulation complete: Security alert recorded.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnThreatDetected(object? sender, ThreatAlert alert)
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            ActiveAlert = new ThreatAlertViewModel(alert);
            _trayService.ShowNotification(
                "⚠ SECURITY ALERT",
                $"Suspicious file activity detected in {alert.FolderPath}! Process: {alert.ProcessName}",
                System.Windows.Forms.ToolTipIcon.Warning);

            _ = LoadSecurityEventsAsync();
        });
    }

    private async Task LoadUpdateSettingsAsync()
    {
        try
        {
            string? auto = await _databaseService.GetSettingAsync("AutoCheckUpdates", "true");
            _autoCheckUpdates = !string.Equals(auto, "false", StringComparison.OrdinalIgnoreCase);
            OnPropertyChanged(nameof(AutoCheckUpdates));

            _updateCheckFrequency = await _databaseService.GetSettingAsync("UpdateCheckFrequency", "Daily") ?? "Daily";
            OnPropertyChanged(nameof(UpdateCheckFrequency));

            string rawUrl = await _databaseService.GetSettingAsync("UpdateManifestUrl", "https://raw.githubusercontent.com/adilmahboobalam/Secappers/main/latest.json")
                ?? "https://raw.githubusercontent.com/adilmahboobalam/Secappers/main/latest.json";
            _updateManifestUrl = UpdateService.NormalizeManifestUrl(rawUrl);
            OnPropertyChanged(nameof(UpdateManifestUrl));

            string? last = await _databaseService.GetSettingAsync("LastUpdateCheckTime");
            if (!string.IsNullOrEmpty(last) && DateTime.TryParse(last, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            {
                LastUpdateCheckText = dt.ToLocalTime().ToString("MMM dd, yyyy HH:mm");
            }
            else
            {
                LastUpdateCheckText = "Never";
            }
        }
        catch { }
    }

    private async Task ExecuteSimulateUpdateAsync()
    {
        try
        {
            var updateInfo = await _updateService.GetSimulatedUpdateInfoAsync();
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var dialog = new UpdateAvailableDialog(_updateService, updateInfo)
                {
                    Owner = Application.Current?.MainWindow
                };
                dialog.ShowDialog();
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to simulate update: {ex.Message}", "Update Simulation", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task ExecuteCheckForUpdatesAsync(bool isManual)
    {
        if (IsCheckingForUpdates) return;
        IsCheckingForUpdates = true;
        UpdateStatusMessage = "Checking for updates...";

        try
        {
            var checkResult = await _updateService.CheckForUpdatesAsync(isManual);
            LastUpdateCheckText = DateTime.Now.ToString("MMM dd, yyyy HH:mm");

            if (checkResult.IsUpdateAvailable && checkResult.Update != null)
            {
                UpdateStatusMessage = $"Update Available: v{checkResult.Update.Version}";

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    var dialog = new UpdateAvailableDialog(_updateService, checkResult.Update)
                    {
                        Owner = Application.Current?.MainWindow
                    };
                    dialog.ShowDialog();
                });
            }
            else
            {
                if (checkResult.ErrorMessage != null && isManual)
                {
                    UpdateStatusMessage = checkResult.ErrorMessage;
                    MessageBox.Show(checkResult.ErrorMessage, "Update Check", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    UpdateStatusMessage = $"You're up to date! SecApper v{CurrentAppVersion} is the latest version.";
                    if (isManual)
                    {
                        MessageBox.Show($"You're up to date!\n\nSecApper Folder Locker v{CurrentAppVersion} is currently the latest version.", "Check for Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            UpdateStatusMessage = $"Update check error: {ex.Message}";
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }
}
