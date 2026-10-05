using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using SecApper.FolderLocker.Services;
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
    private VueBridgeController? _bridge;
    private bool _isExplicitExit;

    public MainWindow()
    {
        InitializeComponent();

        // 1. Initialize Real Security Backend
        _db = new SqliteDatabaseService();
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

        // 2. Active Explorer Double-Click Interceptor
        _accessMonitor = new LockedFolderAccessMonitorService(_db, action => Dispatcher.Invoke(action));
        _accessMonitor.FolderUnlockRequested += folder =>
        {
            Dispatcher.Invoke(() =>
            {
                RestoreWindow();
                _bridge?.SendEvent("unlockRequested", new
                {
                    id = folder.Id,
                    folderPath = folder.FolderPath,
                    folderName = folder.FolderName,
                    status = folder.Status.ToString()
                });
            });
        };

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
                _trayService);

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

    private async Task HandleCommandLineArgsAsync()
    {
        try
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                string targetPath = args[1];
                if (targetPath.Equals("--unlock", StringComparison.OrdinalIgnoreCase) && args.Length > 2)
                {
                    targetPath = args[2];
                }

                targetPath = targetPath.Trim('\"');
                if (Directory.Exists(targetPath) || Path.IsPathRooted(targetPath))
                {
                    var folders = await _db.GetAllFoldersAsync();
                    var folder = folders.FirstOrDefault(f => f.FolderPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase) ||
                                                            f.FolderPath.TrimEnd('\\').Equals(targetPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
                    if (folder != null && folder.Status == FolderStatus.Locked)
                    {
                        _bridge?.SendEvent("unlockRequested", new
                        {
                            id = folder.Id,
                            folderPath = folder.FolderPath,
                            folderName = folder.FolderName,
                            status = folder.Status.ToString()
                        });
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
        _trayService.Dispose();
        base.OnClosing(e);
    }
}