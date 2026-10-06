using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    private DateTime _lastDbFetch = DateTime.MinValue;
    private List<FolderRecord> _cachedLockedFolders = new();

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // High responsiveness polling (80ms) for instant double-click interception
                await Task.Delay(80, ct);

                // Fetch locked folders at most once per second to prevent SQLite contention
                if ((DateTime.UtcNow - _lastDbFetch).TotalMilliseconds > 1000 || _cachedLockedFolders.Count == 0)
                {
                    var allFolders = await _db.GetAllFoldersAsync();
                    _cachedLockedFolders = allFolders.Where(f => f.Status == FolderStatus.Locked).ToList();
                    _lastDbFetch = DateTime.UtcNow;
                }

                if (_cachedLockedFolders.Count == 0) continue;

                // Look for Windows Explorer permission denial / TaskDialog prompts
                ScanWindowsForAccessDenied(_cachedLockedFolders);
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
            try
            {
                if (!IsWindowVisible(hWnd)) return true;

            var sbClass = new StringBuilder(256);
            GetClassName(hWnd, sbClass, sbClass.Capacity);
            string className = sbClass.ToString();

            // Dialog class is #32770, TaskDialogWindow, DirectUIHWND, or other dialog variants
            if (!className.Equals("#32770", StringComparison.OrdinalIgnoreCase) &&
                !className.Equals("TaskDialogWindow", StringComparison.OrdinalIgnoreCase) &&
                !className.Equals("DirectUIHWND", StringComparison.OrdinalIgnoreCase) &&
                !className.Contains("Dialog", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0) return true;

            bool isExplorer = false;
            IntPtr hProcess = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pid);
            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    var sbExe = new StringBuilder(1024);
                    int size = sbExe.Capacity;
                    if (QueryFullProcessImageName(hProcess, 0, sbExe, ref size))
                    {
                        isExplorer = sbExe.ToString().IndexOf("explorer", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }

            if (!isExplorer)
            {
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    isExplorer = proc.ProcessName.Contains("explorer", StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                }
            }

            var sbTitle = new StringBuilder(512);
            GetWindowText(hWnd, sbTitle, sbTitle.Capacity);
            string title = sbTitle.ToString().Trim();

            // Extract dialog structure & text from all child controls fast via Win32
            bool hasDirectUi = false;
            bool hasContinueOrCancel = false;
            var childTextSb = new StringBuilder();

            EnumChildWindows(hWnd, (childHwnd, _) =>
            {
                var sbChildClass = new StringBuilder(64);
                GetClassName(childHwnd, sbChildClass, sbChildClass.Capacity);
                string childClass = sbChildClass.ToString();
                if (childClass.Equals("DirectUIHWND", StringComparison.OrdinalIgnoreCase) ||
                    childClass.Equals("CtrlNotifySink", StringComparison.OrdinalIgnoreCase))
                {
                    hasDirectUi = true;
                }

                var sbText = new StringBuilder(512);
                GetWindowText(childHwnd, sbText, sbText.Capacity);
                string cText = sbText.ToString().Trim();
                if (string.IsNullOrEmpty(cText))
                {
                    SendMessage(childHwnd, WM_GETTEXT, (IntPtr)sbText.Capacity, sbText);
                    cText = sbText.ToString().Trim();
                }

                if (!string.IsNullOrEmpty(cText))
                {
                    childTextSb.Append(cText).Append(' ');
                    if (cText.Contains("Continue", StringComparison.OrdinalIgnoreCase) ||
                        cText.Contains("Cancel", StringComparison.OrdinalIgnoreCase) ||
                        cText.Contains("OK", StringComparison.OrdinalIgnoreCase))
                    {
                        hasContinueOrCancel = true;
                    }
                }
                return true;
            }, IntPtr.Zero);

            string allText = title + " " + childTextSb.ToString();

            bool isPermissionOrDeniedDialog =
                allText.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
                allText.Contains("access", StringComparison.OrdinalIgnoreCase) ||
                allText.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
                allText.Contains("not accessible", StringComparison.OrdinalIgnoreCase) ||
                allText.Contains("permanently", StringComparison.OrdinalIgnoreCase) ||
                allText.Contains("administrator", StringComparison.OrdinalIgnoreCase) ||
                allText.Contains("Location is not available", StringComparison.OrdinalIgnoreCase) ||
                (hasDirectUi && hasContinueOrCancel);

            foreach (var folder in lockedFolders)
            {
                string fullPath = folder.FolderPath.TrimEnd('\\', '/');
                string folderName = folder.FolderName;
                if (string.IsNullOrEmpty(folderName))
                {
                    folderName = Path.GetFileName(fullPath);
                }

                // Match by path or folder name
                bool allTextContainsPath = allText.Contains(fullPath, StringComparison.OrdinalIgnoreCase);
                bool allTextContainsName = !string.IsNullOrEmpty(folderName) &&
                                           allText.Contains(folderName, StringComparison.OrdinalIgnoreCase);

                bool titleMatchesExactName = string.Equals(title, folderName, StringComparison.OrdinalIgnoreCase);
                bool titleMatchesPath = string.Equals(title, fullPath, StringComparison.OrdinalIgnoreCase) ||
                                        title.Contains(fullPath, StringComparison.OrdinalIgnoreCase);
                bool titleContainsName = !string.IsNullOrEmpty(folderName) &&
                                         title.Contains(folderName, StringComparison.OrdinalIgnoreCase);

                bool matches = false;
                if (allTextContainsPath)
                {
                    matches = true;
                }
                else if (allTextContainsName && (isPermissionOrDeniedDialog || isExplorer))
                {
                    matches = true;
                }
                else if (titleMatchesExactName && (isPermissionOrDeniedDialog || hasDirectUi || isExplorer))
                {
                    matches = true;
                }
                else if (titleMatchesPath)
                {
                    matches = true;
                }
                else if (titleContainsName && (isPermissionOrDeniedDialog || hasDirectUi || isExplorer))
                {
                    matches = true;
                }
                else if (lockedFolders.Count == 1 && (isPermissionOrDeniedDialog || hasDirectUi || isExplorer))
                {
                    if (title.Contains("Location", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains("available", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains("Access", StringComparison.OrdinalIgnoreCase) ||
                        allText.Contains("Location", StringComparison.OrdinalIgnoreCase))
                    {
                        matches = true;
                    }
                }

                if (matches)
                {
                    // Debounce repeated triggers for same folder within 2 seconds
                    if (DateTime.UtcNow - _lastPopupTime < TimeSpan.FromSeconds(2) && _lastHandledFolder == fullPath)
                    {
                        DismissDialog(hWnd);
                        return true;
                    }

                    _lastPopupTime = DateTime.UtcNow;
                    _lastHandledFolder = fullPath;

                    // 1. Instantly hide and dismiss the Windows Explorer dialog
                    DismissDialog(hWnd);

                    // 2. Dispatch SecApper unlock popup
                    _dispatchToUi(() =>
                    {
                        FolderUnlockRequested?.Invoke(folder);
                    });

                    return false; // Handled
                }
            }

            return true;
        }
        catch
        {
            return true;
        }
    }, IntPtr.Zero);
    }

    private static void DismissDialog(IntPtr hWnd)
    {
        try
        {
            // 1. Immediately hide window from user's display
            ShowWindow(hWnd, SW_HIDE);

            // 2. Click Cancel child button to abort Windows Explorer elevation
            EnumChildWindows(hWnd, (childHwnd, _) =>
            {
                var sbText = new StringBuilder(64);
                GetWindowText(childHwnd, sbText, sbText.Capacity);
                string bText = sbText.ToString().Trim();
                if (bText.Equals("Cancel", StringComparison.OrdinalIgnoreCase) ||
                    bText.Equals("OK", StringComparison.OrdinalIgnoreCase))
                {
                    PostMessage(childHwnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                }
                return true;
            }, IntPtr.Zero);

            // 3. Send IDCANCEL (2) and WM_CLOSE
            PostMessage(hWnd, WM_COMMAND, (IntPtr)2 /*IDCANCEL*/, IntPtr.Zero);
            PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        Stop();
    }

    // Win32 Imports
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_COMMAND = 0x0111;
    private const uint WM_GETTEXT = 0x000D;
    private const uint BM_CLICK = 0x00F5;
    private const int SW_HIDE = 0;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, StringBuilder lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder text, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
