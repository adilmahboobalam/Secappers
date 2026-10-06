using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using SecApper.FolderLocker.Services;
using SecApper.FolderLocker.ViewModels;
using SecApper.FolderLocker.Views;
using SecApper.Security.Data;
using SecApper.Security.Models;
using SecApper.Security.Ransomware;
using SecApper.Security.Recovery;
using SecApper.Security.Security;
using SecApper.Security.Services;
using SecApper.Security.Updates;

namespace SecApper.FolderLocker;

public partial class MainWindow : Window
{
    private readonly IDatabaseService _db;
    private readonly IPasswordService _passwordService;
    private readonly IMasterPinService _masterPinService;
    private readonly IAclService _aclService;
    private readonly IPermissionBackupService _backupService;
    private readonly IFolderIconService _iconService;
    private readonly IFolderLockService _lockService;
    private readonly IRecoveryService _recoveryService;
    private readonly IThreatScoringService _threatScorer;
    private readonly IProcessMonitorService _processMonitor;
    private readonly IRansomwareProtectionService _ransomwareService;
    private readonly IFolderPathValidator _pathValidator;
    private readonly IExplorerIntegrationService _explorerService;
    private readonly SystemTrayService _trayService;
    private readonly IUpdateService _updateService;
    private readonly ILockedFolderAccessMonitorService _accessMonitor;
    private readonly IExplorerWindowMonitorService _explorerWindowMonitor;
    private UnlockDialog? _activeUnlockDialog;
    private string? _activeUnlockFolderId;
    private VueBridgeController? _bridge;
    private bool _isExplicitExit;

    public MainWindow()
    {
        InitializeComponent();

        // 1. Initialize Real Security Backend
        _db = new SqliteDatabaseService();
        try
        {
            _db.InitializeAsync().GetAwaiter().GetResult();
        }
        catch
        {
        }
        _passwordService = new PasswordService();
        _masterPinService = new MasterPinService(_db, _passwordService);
        _aclService = new AclService();
        _backupService = new PermissionBackupService(_aclService, _db);
        _iconService = new FolderIconService();
        _lockService = new FolderLockService(_db, _aclService, _backupService, _passwordService, _iconService, _masterPinService);
        _recoveryService = new RecoveryService(_db, _aclService, _iconService);
        _threatScorer = new ThreatScoringService();
        _processMonitor = new ProcessMonitorService();
        _ransomwareService = new RansomwareProtectionService(_db, _lockService, _threatScorer, _processMonitor);
        _pathValidator = new FolderPathValidator(AppDomain.CurrentDomain.BaseDirectory, Path.GetDirectoryName(_db.DatabasePath));
        _explorerService = new ExplorerIntegrationService();
        _trayService = new SystemTrayService();
        _updateService = new UpdateService(_db);

        // Ensure Windows Explorer context menus are active
        try
        {
            _explorerService.EnableContextMenu();
        }
        catch
        {
        }

        // 2. Active Explorer Window Tracking Service (Auto re-lock folder when Explorer window closes)
        _explorerWindowMonitor = new ExplorerWindowMonitorService(action => Dispatcher.Invoke(action));
        _explorerWindowMonitor.FolderWindowClosed += async (folderId, folderPath) =>
        {
            try
            {
                var folder = await _db.GetFolderByIdAsync(folderId);
                if (folder == null || folder.Status == FolderStatus.Locked)
                {
                    return;
                }

                var lockResult = await _lockService.LockFolderAsync(folderId);
                if (lockResult.Success)
                {
                    _trayService.ShowNotification(
                        "SecApper Auto-Lock",
                        $"Folder '{folder.FolderName}' has been automatically re-locked because its window was closed.");

                    _bridge?.SendEvent("folderChanged", new
                    {
                        id = folder.Id,
                        status = FolderStatus.Locked.ToString()
                    });

                    await _db.AddSecurityEventAsync(new SecurityEvent
                    {
                        FolderId = folder.Id,
                        EventType = "AutoLockOnWindowClose",
                        Severity = EventSeverity.Info,
                        Description = $"Folder '{folder.FolderName}' automatically re-locked because its Windows Explorer window was closed.",
                        ActionTaken = "Auto-Locked"
                    });
                }
            }
            catch
            {
            }
        };

        // 3. Active Explorer Double-Click Interceptor
        _accessMonitor = new LockedFolderAccessMonitorService(_db, action => Dispatcher.Invoke(action));
        _accessMonitor.FolderUnlockRequested += async folder =>
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                await PromptUnlockPopupAsync(folder);
            });
        };
        _accessMonitor.Start();

        _trayService.OpenRequested += RestoreWindow;
        _trayService.ExitRequested += () =>
        {
            _isExplicitExit = true;
            Close();
        };

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Initialize WebView2
            await webView.EnsureCoreWebView2Async();

            _bridge = new VueBridgeController(
                this,
                webView,
                _db,
                _lockService,
                _recoveryService,
                _ransomwareService,
                _updateService,
                _masterPinService,
                _passwordService,
                _pathValidator,
                _trayService,
                _explorerWindowMonitor);

            // Connect WebView2 IPC message listener
            webView.CoreWebView2.WebMessageReceived += async (s, args) =>
            {
                await _bridge.HandleMessageAsync(args.WebMessageAsJson);
            };

            // Map Virtual Host to the built Vue 3 / Vite assets
            string wwwrootDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
            if (Directory.Exists(wwwrootDir))
            {
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "app.secapper.local",
                    wwwrootDir,
                    CoreWebView2HostResourceAccessKind.Allow);

                webView.Source = new Uri("https://app.secapper.local/index.html");
            }
            else
            {
                // Development fallback URL
                webView.Source = new Uri("http://localhost:5173/");
            }

            // Start background monitor services
            _accessMonitor.Start();
            _explorerWindowMonitor.Start();

            // Handle CLI args (e.g. Explorer right click --unlock <path>)
            await HandleCommandLineArgsAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error initializing SecApper interface:\n{ex.Message}",
                "SecApper Security",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public async Task PromptUnlockPopupAsync(FolderRecord folder)
    {
        if (_activeUnlockDialog != null && _activeUnlockFolderId == folder.Id)
        {
            _activeUnlockDialog.Activate();
            return;
        }

        var unlockVm = new UnlockDialogViewModel
        {
            FolderName = folder.FolderName,
            FolderPath = folder.FolderPath
        };

        var dialog = new UnlockDialog
        {
            DataContext = unlockVm,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            ShowInTaskbar = true
        };

        _activeUnlockDialog = dialog;
        _activeUnlockFolderId = folder.Id;

        dialog.ValidatePasswordAsync = async pwd =>
        {
            var latest = await _db.GetFolderByIdAsync(folder.Id);
            if (latest == null) return "Folder record not found.";

            bool ok = _passwordService.VerifyPassword(pwd, latest.PasswordHash, latest.PasswordSalt, latest.PasswordAlgorithm, latest.PasswordIterations);
            if (!ok && _masterPinService != null)
            {
                ok = await _masterPinService.VerifyMasterPinAsync(pwd);
            }

            if (!ok)
            {
                return "Incorrect password or Master PIN.";
            }

            return null;
        };

        try
        {
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                var unlockResult = await _lockService.UnlockFolderAsync(folder.Id, unlockVm.Password);
                if (unlockResult.Success)
                {
                    _trayService.ShowNotification(
                        "Folder Unlocked",
                        $"'{folder.FolderName}' has been unlocked.");

                    _bridge?.SendEvent("folderChanged", new
                    {
                        id = folder.Id,
                        status = FolderStatus.Unlocked.ToString()
                    });

                    // Open folder in Windows Explorer
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = folder.FolderPath,
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                    }

                    // Track window to automatically re-lock when window closes!
                    _explorerWindowMonitor.TrackFolderWindow(folder.Id, folder.FolderPath);
                }
            }
        }
        finally
        {
            _activeUnlockDialog = null;
            _activeUnlockFolderId = null;
        }
    }

    public async Task HandleCommandLineArgsAsync(string[]? customArgs = null)
    {
        try
        {
            string[] rawArgs = customArgs ?? Environment.GetCommandLineArgs();
            // If customArgs came from e.Args, index 0 is first arg; if Environment, index 0 is exe path
            var argsList = new List<string>(rawArgs);
            if (customArgs == null && argsList.Count > 0)
            {
                argsList.RemoveAt(0);
            }

            if (argsList.Count > 0)
            {
                string targetPath = argsList[0];
                if (targetPath.Equals("--unlock", StringComparison.OrdinalIgnoreCase) && argsList.Count > 1)
                {
                    targetPath = argsList[1];
                }

                targetPath = targetPath.Trim('\"');
                if (Directory.Exists(targetPath) || Path.IsPathRooted(targetPath))
                {
                    var folders = await _db.GetAllFoldersAsync();
                    var folder = folders.FirstOrDefault(f => f.FolderPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase) ||
                                                            f.FolderPath.TrimEnd('\\').Equals(targetPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
                    if (folder != null && folder.Status == FolderStatus.Locked)
                    {
                        await PromptUnlockPopupAsync(folder);
                    }
                }
            }
        }
        catch
        {
        }
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public void ShutdownApp()
    {
        _isExplicitExit = true;
        try { _accessMonitor.Stop(); } catch { }
        try { _accessMonitor.Dispose(); } catch { }
        try { _explorerWindowMonitor.Stop(); } catch { }
        try { _explorerWindowMonitor.Dispose(); } catch { }
        try { _trayService.Dispose(); } catch { }
        Close();
        Environment.Exit(0);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            // Minimize to system tray instead of exiting so double-click interception and ransomware monitoring remain active
            e.Cancel = true;
            Hide();
            _trayService.ShowNotification(
                "SecApper Folder Locker",
                "Protection active in background. Double-click unlock popup and folder security are running.");
            return;
        }

        _accessMonitor.Stop();
        _accessMonitor.Dispose();
        _explorerWindowMonitor.Stop();
        _explorerWindowMonitor.Dispose();
        _trayService.Dispose();
        base.OnClosing(e);
    }
}