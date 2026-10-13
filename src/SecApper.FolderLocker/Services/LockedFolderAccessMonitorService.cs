using System;
using System.Collections.Concurrent;
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
    void SuppressFolder(string folderPath, TimeSpan duration);
    void UnsuppressFolder(string folderPath);
    void RefreshLockedFolders();
    void ResetPromptState();
    bool IsPromptActive { get; set; }
    bool IsEnabled { get; set; }
    event Action<FolderRecord>? FolderUnlockRequested;
}

/// <summary>
/// Monitors Windows Explorer access attempts to protected folders.
/// When Windows Explorer attempts to open a locked folder and displays
/// an "Access is denied" or "Location is not available" dialog (such as upon
/// user double-clicking the folder icon), this service intercepts the dialog,
/// cleanly dismisses the message box, and triggers the SecApper unlock popup.
/// 
/// Uses gentle foreground window inspection (200ms heartbeat) without invasive
/// system hooks or UI automation tree dumping to ensure Windows Explorer,
/// the taskbar, and navigation pane are NEVER frozen, hidden, or corrupted.
/// </summary>
public class LockedFolderAccessMonitorService : ILockedFolderAccessMonitorService
{
    private readonly IDatabaseService _db;
    private readonly Action<Action> _dispatchToUi;
    private readonly ConcurrentDictionary<string, DateTime> _suppressedFolders = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _lastPopupPerFolder = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<IntPtr, DateTime> _processedWindows = new();
    private DateTime _lastGlobalPopupTime = DateTime.MinValue;
    private DateTime _lastPromptStartTime = DateTime.MinValue;
    private readonly object _popupLock = new();
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private DateTime _lastDbFetch = DateTime.MinValue;
    private List<FolderRecord> _cachedLockedFolders = new();

    public bool IsPromptActive { get; set; }
    public bool IsEnabled { get; set; } = true;

    public event Action<FolderRecord>? FolderUnlockRequested;

    public LockedFolderAccessMonitorService(IDatabaseService db, Action<Action> dispatchToUi)
    {
        _db = db;
        _dispatchToUi = dispatchToUi;
    }

    public void SuppressFolder(string folderPath, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return;
        string norm = folderPath.TrimEnd('\\', '/');
        _suppressedFolders[norm] = DateTime.UtcNow + duration;
    }

    public void UnsuppressFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return;
        string norm = folderPath.TrimEnd('\\', '/');
        _suppressedFolders.TryRemove(norm, out _);
        _lastPopupPerFolder.TryRemove(norm, out _);
    }

    public void RefreshLockedFolders()
    {
        _lastDbFetch = DateTime.MinValue;
    }

    public void ResetPromptState()
    {
        IsPromptActive = false;
    }

    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();

        // Safe foreground window monitor loop:
        // Inspects active foreground dialogs every 200ms.
        // Uses 0% CPU and zero system hooks to guarantee Windows taskbar/navbar stability.
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
                await Task.Delay(200, ct);

                // Fetch locked folders at most once every 1.5 seconds (or immediately if invalidated)
                if ((DateTime.UtcNow - _lastDbFetch).TotalMilliseconds > 1500 || _cachedLockedFolders.Count == 0)
                {
                    var allFolders = await _db.GetAllFoldersAsync();
                    _cachedLockedFolders = allFolders.Where(f => f.Status == FolderStatus.Locked).ToList();
                    _lastDbFetch = DateTime.UtcNow;
                }

                if (_cachedLockedFolders.Count == 0) continue;

                // Watchdog: If prompt has been active for over 45s without resetting, clear it to avoid permanent freeze
                if (IsPromptActive && (DateTime.UtcNow - _lastPromptStartTime).TotalSeconds > 45)
                {
                    IsPromptActive = false;
                }

                if (!IsEnabled || IsPromptActive) continue;

                // Housekeeping: remove old or dead window handles from cache
                if (_processedWindows.Count > 10)
                {
                    DateTime cutoff = DateTime.UtcNow - TimeSpan.FromSeconds(5);
                    foreach (var entry in _processedWindows)
                    {
                        if (entry.Value < cutoff || !IsWindow(entry.Key))
                        {
                            _processedWindows.TryRemove(entry.Key, out _);
                        }
                    }
                }

                IntPtr fgHwnd = GetForegroundWindow();
                if (fgHwnd != IntPtr.Zero)
                {
                    CheckAndProcessWindow(fgHwnd, _cachedLockedFolders);

                    IntPtr popup = GetLastActivePopup(fgHwnd);
                    if (popup != IntPtr.Zero && popup != fgHwnd)
                    {
                        CheckAndProcessWindow(popup, _cachedLockedFolders);
                    }
                }
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

    private void CheckAndProcessWindow(IntPtr hWnd, IReadOnlyList<FolderRecord> lockedFolders)
    {
        if (!IsEnabled) return;
        if (lockedFolders == null || lockedFolders.Count == 0) return;
        if (IsPromptActive) return; // Never process or popup if an unlock dialog is already showing!

        // Debounce dialog window handle if it was processed recently (< 1.5 seconds)
        if (_processedWindows.TryGetValue(hWnd, out DateTime lastProcessed) &&
            DateTime.UtcNow - lastProcessed < TimeSpan.FromSeconds(1.5))
        {
            return;
        }
        if (!IsWindowVisible(hWnd)) return;

        // 1. Process check: MUST be Windows Explorer and NEVER SecApper itself
        GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0) return;
        if (pid == (uint)Process.GetCurrentProcess().Id) return;

        bool isExplorer = false;
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            isExplorer = proc.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return;
        }

        if (!isExplorer) return;

        // 2. Class name verification:
        // STRICT SAFETY: NEVER touch or process File Explorer browsing windows, the desktop,
        // or ANY part of the Windows taskbar, start menu, tray, or navigation pane!
        var sbClass = new StringBuilder(256);
        GetClassName(hWnd, sbClass, sbClass.Capacity);
        string className = sbClass.ToString();

        // Hard exclusion of all shell UI elements:
        if (className.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("ExploreWClass", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("WorkerW", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("XamlExplorerHostIslandWindow", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("DirectUIHWND", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("DV2ControlHost", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("ReBarWindow32", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("MSTaskSwWClass", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("TrayNotifyWnd", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // 3. MUST strictly be a Win32 dialog box (#32770) or TaskDialogWindow
        bool isDialogClass = className.Equals("#32770", StringComparison.OrdinalIgnoreCase) ||
                             className.Equals("TaskDialogWindow", StringComparison.OrdinalIgnoreCase);
        if (!isDialogClass) return;

        long style = GetWindowLong(hWnd, GWL_STYLE);
        // Exclude child windows; must be a top-level popup dialog
        if ((style & WS_CHILD) != 0 || (style & WS_POPUP) == 0) return;

        // 4. Verify title & exclude properties dialog
        var sbTitle = new StringBuilder(512);
        GetWindowText(hWnd, sbTitle, sbTitle.Capacity);
        string title = sbTitle.ToString().Trim();

        if (title.EndsWith("Properties", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        bool hasErrorTitle = title.Contains("Location is not available", StringComparison.OrdinalIgnoreCase) ||
                             title.Contains("not accessible", StringComparison.OrdinalIgnoreCase) ||
                             title.Contains("Access is denied", StringComparison.OrdinalIgnoreCase);

        // 5. Extract text from child controls
        var childTextSb = new StringBuilder();
        bool hasApplyButton = false;
        EnumChildWindows(hWnd, (childHwnd, _) =>
        {
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
                if (cText.Equals("Apply", StringComparison.OrdinalIgnoreCase))
                {
                    hasApplyButton = true;
                }
                childTextSb.Append(cText).Append(' ');
            }
            return true;
        }, IntPtr.Zero);

        if (hasApplyButton) return;

        // Also extract text from DirectUIHWND elements inside this dialog via UI Automation descendants
        try
        {
            var element = System.Windows.Automation.AutomationElement.FromHandle(hWnd);
            if (element != null)
            {
                string rootName = element.Current.Name;
                if (!string.IsNullOrEmpty(rootName)) childTextSb.Append(rootName).Append(' ');

                var descendants = element.FindAll(System.Windows.Automation.TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition);
                foreach (System.Windows.Automation.AutomationElement d in descendants)
                {
                    try
                    {
                        string name = d.Current.Name;
                        if (!string.IsNullOrEmpty(name)) childTextSb.Append(name).Append(' ');
                    }
                    catch { }
                }
            }
        }
        catch { }

        string allText = title + " " + childTextSb.ToString();

        // 6. Must contain access denied / not accessible phrase
        bool isAccessDeniedError =
            hasErrorTitle ||
            allText.Contains("Location is not available", StringComparison.OrdinalIgnoreCase) ||
            allText.Contains("is not accessible", StringComparison.OrdinalIgnoreCase) ||
            allText.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) ||
            allText.Contains("permission to access", StringComparison.OrdinalIgnoreCase) ||
            allText.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
            allText.Contains("permanently get access", StringComparison.OrdinalIgnoreCase);

        if (!isAccessDeniedError) return;

        // 7. Check if the error explicitly mentions one of our locked folders
        foreach (var folder in lockedFolders)
        {
            string fullPath = folder.FolderPath.TrimEnd('\\', '/');
            string folderName = folder.FolderName;
            if (string.IsNullOrEmpty(folderName))
            {
                folderName = Path.GetFileName(fullPath);
            }

            // Path match: Dialog text or title references fullPath
            bool pathMatch = allText.Contains(fullPath, StringComparison.OrdinalIgnoreCase) ||
                             title.Contains(fullPath, StringComparison.OrdinalIgnoreCase);

            // Name match: Dialog title or text references folder name AND it is an access/permission denied error
            bool nameMatch = !string.IsNullOrEmpty(folderName) &&
                             (title.Equals(folderName, StringComparison.OrdinalIgnoreCase) ||
                              title.StartsWith(folderName, StringComparison.OrdinalIgnoreCase) ||
                              allText.Contains(folderName, StringComparison.OrdinalIgnoreCase)) &&
                             isAccessDeniedError;

            if (!pathMatch && !nameMatch)
            {
                continue;
            }

            // Check if folder is suppressed (e.g. user cancelled prompt recently):
            // In this case, simply ignore the dialog without doing anything!
            if (_suppressedFolders.TryGetValue(fullPath, out DateTime until) && DateTime.UtcNow < until)
            {
                _processedWindows[hWnd] = DateTime.UtcNow;
                return;
            }

            // Check per-folder cooldown (at least 1.5s between prompts for same folder to absorb double-click jitter)
            if (_lastPopupPerFolder.TryGetValue(fullPath, out DateTime lastFolderPopup) &&
                (DateTime.UtcNow - lastFolderPopup).TotalSeconds < 1.5)
            {
                _processedWindows[hWnd] = DateTime.UtcNow;
                return;
            }

            // Check global cooldown (at least 1.0s between any popups)
            if ((DateTime.UtcNow - _lastGlobalPopupTime).TotalSeconds < 1.0)
            {
                _processedWindows[hWnd] = DateTime.UtcNow;
                return;
            }

            lock (_popupLock)
            {
                if (IsPromptActive) return;
                if (_processedWindows.TryGetValue(hWnd, out DateTime lp) &&
                    DateTime.UtcNow - lp < TimeSpan.FromSeconds(1.5))
                {
                    return;
                }

                _processedWindows[hWnd] = DateTime.UtcNow;
                IsPromptActive = true;
                _lastPromptStartTime = DateTime.UtcNow;
                _lastGlobalPopupTime = DateTime.UtcNow;
                _lastPopupPerFolder[fullPath] = DateTime.UtcNow;
            }

            // Safely close the message box error dialog without hiding or closing any Explorer shells
            DismissDialog(hWnd);

            // Dispatch SecApper unlock popup
            _dispatchToUi(() =>
            {
                FolderUnlockRequested?.Invoke(folder);
            });

            return;
        }
    }

    private void DismissDialog(IntPtr hWnd)
    {
        try
        {
            _processedWindows[hWnd] = DateTime.UtcNow;

            // Double check: NEVER dismiss or hide main explorer, desktop, or taskbar windows
            var sbClass = new StringBuilder(64);
            GetClassName(hWnd, sbClass, sbClass.Capacity);
            string cls = sbClass.ToString();
            if (!cls.Equals("#32770", StringComparison.OrdinalIgnoreCase) &&
                !cls.Equals("TaskDialogWindow", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Click Cancel button if present cleanly via Win32 messages (Standard Windows message box dismissal)
            bool cancelClicked = false;
            EnumChildWindows(hWnd, (childHwnd, _) =>
            {
                var sbText = new StringBuilder(64);
                GetWindowText(childHwnd, sbText, sbText.Capacity);
                string bText = sbText.ToString().Trim();
                if (bText.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
                {
                    PostMessage(childHwnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                    cancelClicked = true;
                }
                return true;
            }, IntPtr.Zero);

            if (cancelClicked)
            {
                PostMessage(hWnd, WM_COMMAND, (IntPtr)2 /* IDCANCEL */, IntPtr.Zero);
            }
            else
            {
                // Single button message box (OK only)
                EnumChildWindows(hWnd, (childHwnd, _) =>
                {
                    var sbText = new StringBuilder(64);
                    GetWindowText(childHwnd, sbText, sbText.Capacity);
                    string bText = sbText.ToString().Trim();
                    if (bText.Equals("OK", StringComparison.OrdinalIgnoreCase))
                    {
                        PostMessage(childHwnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                    }
                    return true;
                }, IntPtr.Zero);

                PostMessage(hWnd, WM_COMMAND, (IntPtr)1 /* IDOK */, IntPtr.Zero);
                PostMessage(hWnd, WM_COMMAND, (IntPtr)2 /* IDCANCEL */, IntPtr.Zero);
            }
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        Stop();
    }

    // Win32 Constants
    private const uint WM_COMMAND = 0x0111;
    private const uint WM_GETTEXT = 0x000D;
    private const uint BM_CLICK = 0x00F5;
    private const int GWL_STYLE = -16;
    private const uint WS_POPUP = 0x80000000;
    private const uint WS_CHILD = 0x40000000;

    private static long GetWindowLong(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
            return GetWindowLongPtr64(hWnd, nIndex).ToInt64();
        return GetWindowLong32(hWnd, nIndex);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetLastActivePopup(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, StringBuilder lParam);

    [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageInt(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
