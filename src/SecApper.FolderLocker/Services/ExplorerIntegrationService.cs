using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace SecApper.FolderLocker.Services;

public interface IExplorerIntegrationService
{
    bool IsContextMenuEnabled();
    bool EnableContextMenu();
    bool DisableContextMenu();
    bool SetStartWithWindows(bool enabled);
    bool IsStartWithWindowsEnabled();
}

public class ExplorerIntegrationService : IExplorerIntegrationService
{
    private const string ShellKeyPath = @"Software\Classes\Directory\shell\SecApperFolderLocker";
    private const string UnlockKeyPath = @"Software\Classes\Directory\shell\SecApperUnlock";
    private const string BackgroundKeyPath = @"Software\Classes\Directory\Background\shell\SecApperFolderLocker";
    private const string DriveKeyPath = @"Software\Classes\Drive\shell\SecApperFolderLocker";
    private const string AppPathsKeyPath = @"Software\Microsoft\Windows\CurrentVersion\App Paths\SecApper.FolderLocker.exe";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "SecApperFolderLocker";

    public bool IsContextMenuEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ShellKeyPath);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    public bool EnableContextMenu()
    {
        try
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName 
                             ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SecApper.FolderLocker.exe");
            string appDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;

            // 1. General Folder Locker Menu (Directory)
            using (var key = Registry.CurrentUser.CreateSubKey(ShellKeyPath))
            {
                if (key != null)
                {
                    key.SetValue("", "SecApper Folder Locker");
                    key.SetValue("Icon", $"\"{exePath}\"");
                    using var cmdKey = key.CreateSubKey("command");
                    cmdKey?.SetValue("", $"\"{exePath}\" \"%1\"");
                }
            }

            // 2. Direct Unlock Context Menu (Directory)
            using (var unlockKey = Registry.CurrentUser.CreateSubKey(UnlockKeyPath))
            {
                if (unlockKey != null)
                {
                    unlockKey.SetValue("", "🔓 Unlock with SecApper");
                    unlockKey.SetValue("Icon", $"\"{exePath}\"");
                    using var cmdKey = unlockKey.CreateSubKey("command");
                    cmdKey?.SetValue("", $"\"{exePath}\" --unlock \"%1\"");
                }
            }

            // 3. Folder Background Context Menu (Directory\Background)
            using (var bgKey = Registry.CurrentUser.CreateSubKey(BackgroundKeyPath))
            {
                if (bgKey != null)
                {
                    bgKey.SetValue("", "SecApper Folder Locker");
                    bgKey.SetValue("Icon", $"\"{exePath}\"");
                    using var cmdKey = bgKey.CreateSubKey("command");
                    cmdKey?.SetValue("", $"\"{exePath}\" \"%V\"");
                }
            }

            // 4. Drive Context Menu (Drive)
            using (var driveKey = Registry.CurrentUser.CreateSubKey(DriveKeyPath))
            {
                if (driveKey != null)
                {
                    driveKey.SetValue("", "SecApper Folder Locker");
                    driveKey.SetValue("Icon", $"\"{exePath}\"");
                    using var cmdKey = driveKey.CreateSubKey("command");
                    cmdKey?.SetValue("", $"\"{exePath}\" \"%1\"");
                }
            }

            // 5. App Paths Registration for Windows Run command
            using (var appPathKey = Registry.CurrentUser.CreateSubKey(AppPathsKeyPath))
            {
                if (appPathKey != null)
                {
                    appPathKey.SetValue("", exePath);
                    appPathKey.SetValue("Path", appDir);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool DisableContextMenu()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(ShellKeyPath, false);
            Registry.CurrentUser.DeleteSubKeyTree(UnlockKeyPath, false);
            Registry.CurrentUser.DeleteSubKeyTree(BackgroundKeyPath, false);
            Registry.CurrentUser.DeleteSubKeyTree(DriveKeyPath, false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool SetStartWithWindows(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key != null)
            {
                if (enabled)
                {
                    string exePath = Process.GetCurrentProcess().MainModule?.FileName 
                                     ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SecApper.FolderLocker.exe");
                    key.SetValue(RunValueName, $"\"{exePath}\" --background");
                }
                else
                {
                    key.DeleteValue(RunValueName, false);
                }
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public bool IsStartWithWindowsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(RunValueName) != null;
        }
        catch
        {
            return false;
        }
    }
}
