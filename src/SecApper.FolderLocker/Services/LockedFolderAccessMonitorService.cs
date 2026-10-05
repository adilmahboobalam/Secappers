using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SecApper.Security.Data;
using SecApper.Security.Models;

namespace SecApper.FolderLocker.Services;

public interface ILockedFolderAccessMonitorService : IDisposable
{
    void Start();
    void Stop();
    event Action<FolderRecord>? FolderUnlockRequested;
}

/// <summary>
/// Monitors Windows Explorer access attempts to protected folders.
/// When Windows Explorer attempts to open a locked folder and displays
/// an "Access is denied" or "Location is not available" dialog, this service
/// intercepts the dialog, closes it, and triggers the SecApper unlock popup.
/// </summary>
public class LockedFolderAccessMonitorService : ILockedFolderAccessMonitorService
{
    private readonly IDatabaseService _db;
    private readonly Action<Action> _dispatchToUi;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private DateTime _lastPopupTime = DateTime.MinValue;
    private string? _lastHandledFolder;

    public event Action<FolderRecord>? FolderUnlockRequested;

    public LockedFolderAccessMonitorService(IDatabaseService db, Action<Action> dispatchToUi)
    {
        _db = db;
        _dispatchToUi = dispatchToUi;
    }

    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        _monitorTask = Task.Run(() => MonitorLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(250, ct);

                // Fetch currently locked folder paths
                var allFolders = await _db.GetAllFoldersAsync();
                var lockedFolders = allFolders.Where(f => f.Status == FolderStatus.Locked).ToList();
                if (lockedFolders.Count == 0) continue;

                // Look for Windows Explorer "Access is denied" / "Location is not available" dialogs
                ScanWindowsForAccessDenied(lockedFolders);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Resilient loop
            }
        }
    }

    private void ScanWindowsForAccessDenied(IReadOnlyList<FolderRecord> lockedFolders)
    {
        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            var sbClass = new StringBuilder(256);
            GetClassName(hWnd, sbClass, sbClass.Capacity);
            string className = sbClass.ToString();

            // Dialog class is #32770
            if (className == "#32770")
            {
                var sbTitle = new StringBuilder(512);
                GetWindowText(hWnd, sbTitle, sbTitle.Capacity);
                string title = sbTitle.ToString();

                // Get all child text in the dialog (error message label)
                string dialogText = GetAllChildText(hWnd);
                string combined = title + " " + dialogText;

                foreach (var folder in lockedFolders)
                {
                    string path = folder.FolderPath.TrimEnd('\\');
                    string folderName = Path.GetFileName(path);

                    bool matchesPath = combined.Contains(path, StringComparison.OrdinalIgnoreCase);
                    bool matchesNameAndDenied = !string.IsNullOrEmpty(folderName) &&
                                                combined.Contains(folderName, StringComparison.OrdinalIgnoreCase) &&
                                                (combined.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) ||
                                                 combined.Contains("Location is not available", StringComparison.OrdinalIgnoreCase) ||
                                                 combined.Contains("not accessible", StringComparison.OrdinalIgnoreCase) ||
                                                 combined.Contains("denied permission", StringComparison.OrdinalIgnoreCase) ||
                                                 combined.Contains("permission to access", StringComparison.OrdinalIgnoreCase));

                    if (matchesPath || matchesNameAndDenied)
                    {
                        // Avoid triggering repeatedly within 2 seconds for the same folder
                        if (DateTime.UtcNow - _lastPopupTime < TimeSpan.FromSeconds(2) && _lastHandledFolder == path)
                        {
                            // Still close duplicate error dialogs
                            PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            return true;
                        }

                        _lastPopupTime = DateTime.UtcNow;
                        _lastHandledFolder = path;

                        // 1. Instantly close the Windows Explorer error dialog
                        PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

                        // 2. Dispatch the SecApper unlock popup
                        _dispatchToUi(() =>
                        {
                            FolderUnlockRequested?.Invoke(folder);
                        });

                        return false; // Handled
                    }
                }
            }

            return true;
        }, IntPtr.Zero);
    }

    private string GetAllChildText(IntPtr parentHwnd)
    {
        var sb = new StringBuilder();
        EnumChildWindows(parentHwnd, (childHwnd, lParam) =>
        {
            var text = new StringBuilder(512);
            GetWindowText(childHwnd, text, text.Capacity);
            if (text.Length > 0)
            {
                sb.Append(text).Append(' ');
            }
            return true;
        }, IntPtr.Zero);
        return sb.ToString();
    }

    public void Dispose()
    {
        Stop();
    }

    // Win32 Imports
    private const uint WM_CLOSE = 0x0010;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
}
