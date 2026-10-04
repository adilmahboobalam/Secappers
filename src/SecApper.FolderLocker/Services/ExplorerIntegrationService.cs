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
}

public class ExplorerIntegrationService : IExplorerIntegrationService
{
    private const string ShellKeyPath = @"Software\Classes\Directory\shell\SecApperFolderLocker";
    private const string UnlockKeyPath = @"Software\Classes\Directory\shell\SecApperUnlock";

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

            // 1. General Folder Locker Menu
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

            // 2. Direct Unlock Context Menu
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
            return true;
        }
        catch
        {
            return false;
        }
    }
}
