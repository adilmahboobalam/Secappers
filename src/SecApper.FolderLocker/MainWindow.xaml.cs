using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
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
    private readonly MainViewModel _viewModel;
    private readonly SystemTrayService _trayService;
    private readonly ILockedFolderAccessMonitorService _accessMonitor;
    private readonly IDatabaseService _db;
    private bool _isExplicitExit;

    public MainWindow()
    {
        InitializeComponent();

        // Instantiate services
        _db = new SqliteDatabaseService();
        var passwordService = new PasswordService();
        var masterPinService = new MasterPinService(_db, passwordService);
        var aclService = new AclService();
        var backupService = new PermissionBackupService(aclService, _db);
        var iconService = new FolderIconService();
        var lockService = new FolderLockService(_db, aclService, backupService, passwordService, iconService, masterPinService);
        var recoveryService = new RecoveryService(_db, aclService, iconService);
        var threatScorer = new ThreatScoringService();
        var processMonitor = new ProcessMonitorService();
        var ransomwareService = new RansomwareProtectionService(_db, lockService, threatScorer, processMonitor);
        var pathValidator = new FolderPathValidator(AppDomain.CurrentDomain.BaseDirectory, Path.GetDirectoryName(_db.DatabasePath));
        var explorerService = new ExplorerIntegrationService();
        _trayService = new SystemTrayService();
        var updateService = new UpdateService(_db);

        // Ensure Windows Explorer context menus ("🔓 Unlock with SecApper") are registered
        try
        {
            explorerService.EnableContextMenu();
        }
        catch
        {
        }

        _viewModel = new MainViewModel(
            _db,
            lockService,
            recoveryService,
            ransomwareService,
            pathValidator,
            passwordService,
            explorerService,
            _trayService,
            updateService,
            masterPinService);

        DataContext = _viewModel;

        // Initialize active access monitor to intercept double-click attempts in Windows Explorer
        _accessMonitor = new LockedFolderAccessMonitorService(_db, action => Dispatcher.Invoke(action));
        _accessMonitor.FolderUnlockRequested += async folder =>
        {
            await _viewModel.PromptUnlockForFolderAsync(folder);
        };

        _trayService.OpenRequested += RestoreWindow;
        _trayService.ExitRequested += () =>
        {
            _isExplicitExit = true;
            Close();
        };

        Loaded += async (s, e) =>
        {
            // Prompt for Master PIN setup on fresh installation if not yet configured
            if (!await masterPinService.IsMasterPinConfiguredAsync())
            {
                var setupDialog = new SetupMasterPinDialog(masterPinService)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                bool? setupResult = setupDialog.ShowDialog();
                if (setupResult != true && !await masterPinService.IsMasterPinConfiguredAsync())
                {
                    _isExplicitExit = true;
                    System.Windows.Application.Current.Shutdown();
                    return;
                }
            }

            await _viewModel.InitializeAsync();
            _accessMonitor.Start();

            // Check if application was launched with a folder path or --unlock argument
            await HandleCommandLineArgsAsync();
        };
    }

    private async System.Threading.Tasks.Task HandleCommandLineArgsAsync()
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
                        await _viewModel.PromptUnlockForFolderAsync(folder);
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
            // Minimize to system tray instead of exiting immediately so double-click interception and ransomware protection stay active
            e.Cancel = true;
            Hide();
            _trayService.ShowNotification("SecApper Folder Locker", "Application running in background. Double-click unlock popup and folder security are active.");
            return;
        }

        _accessMonitor.Stop();
        _accessMonitor.Dispose();
        _trayService.Dispose();
        base.OnClosing(e);
    }
}