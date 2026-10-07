using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
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
/// intercepts the dialog, immediately hides/dismisses it, and triggers the
/// SecApper unlock popup.
/// </summary>
public class LockedFolderAccessMonitorService : ILockedFolderAccessMonitorService
{
    private readonly IDatabaseService _db;
    private readonly Action<Action> _dispatchToUi;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private AutomationEventHandler? _windowOpenedHandler;
    private DateTime _lastPopupTime = DateTime.MinValue;
    private string? _lastHandledFolder;
    private DateTime _lastDbFetch = DateTime.MinValue;
    private List<FolderRecord> _cachedLockedFolders = new();

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

        // 1. Start UI Automation Event Listener for real-time window detection
        try
        {
            _windowOpenedHandler = OnWindowOpened;
            Automation.AddAutomationEventHandler(
                WindowPattern.WindowOpenedEvent,
                AutomationElement.RootElement,
                TreeScope.Children,
                _windowOpenedHandler);
        }
        catch
        {
        }

        // 2. Start high-frequency background polling loop as fail-safe guarantee
        _monitorTask = Task.Run(() => MonitorLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        try
        {
            if (_windowOpenedHandler != null)
            {
                Automation.RemoveAutomationEventHandler(
                    WindowPattern.WindowOpenedEvent,
                    AutomationElement.RootElement,
                    _windowOpenedHandler);
                _windowOpenedHandler = null;
            }
        }
        catch
        {
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private void OnWindowOpened(object sender, AutomationEventArgs e)
    {
        if (sender is AutomationElement element)
        {
            try
            {
                int nativeHandle = element.Current.NativeWindowHandle;
                if (nativeHandle != 0)
                {
                    IntPtr hWnd = new IntPtr(nativeHandle);
                    CheckAndProcessWindow(hWnd, _cachedLockedFolders);
                }
            }
            catch
            {
            }
        }
    }

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Responsive polling interval (60ms) for instant double-click interception
                await Task.Delay(60, ct);

                // Fetch locked folders at most once per second to prevent SQLite contention
                if ((DateTime.UtcNow - _lastDbFetch).TotalMilliseconds > 1000 || _cachedLockedFolders.Count == 0)
                {
                    var allFolders = await _db.GetAllFoldersAsync();
                    _cachedLockedFolders = allFolders.Where(f => f.Status == FolderStatus.Locked).ToList();
                    _lastDbFetch = DateTime.UtcNow;
                }

                if (_cachedLockedFolders.Count == 0) continue;

                // Scan all top-level windows for Windows Explorer permission denial / error prompts
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
                CheckAndProcessWindow(hWnd, lockedFolders);
                return true;
            }
            catch
            {
                return true;
            }
        }, IntPtr.Zero);
    }

    private void CheckAndProcessWindow(IntPtr hWnd, IReadOnlyList<FolderRecord> lockedFolders)
    {
        if (lockedFolders == null || lockedFolders.Count == 0) return;
        if (!IsWindowVisible(hWnd)) return;

        var sbClass = new StringBuilder(256);
        GetClassName(hWnd, sbClass, sbClass.Capacity);
        string className = sbClass.ToString();

        // Check if class matches standard dialog, task dialog, or explorer popup variants
        bool isDialogClass = className.Equals("#32770", StringComparison.OrdinalIgnoreCase) ||
                             className.Equals("TaskDialogWindow", StringComparison.OrdinalIgnoreCase) ||
                             className.Equals("DirectUIHWND", StringComparison.OrdinalIgnoreCase) ||
                             className.Equals("OperationStatusWindow", StringComparison.OrdinalIgnoreCase) ||
                             className.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                             className.Equals("Xaml_WindowedPopupClass", StringComparison.OrdinalIgnoreCase) ||
                             className.Contains("Dialog", StringComparison.OrdinalIgnoreCase);

        GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0) return;

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
                    string exeName = sbExe.ToString();
                    isExplorer = exeName.IndexOf("explorer", StringComparison.OrdinalIgnoreCase) >= 0;
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

        // Window style check: popups from explorer
        int style = GetWindowLong(hWnd, GWL_STYLE);
        bool isPopup = (style & WS_POPUP) != 0;

        if (!isDialogClass && !isPopup && !isExplorer)
        {
            return;
        }

        var sbTitle = new StringBuilder(512);
        GetWindowText(hWnd, sbTitle, sbTitle.Capacity);
        string title = sbTitle.ToString().Trim();

        // Extract Win32 child texts
        var childTextSb = new StringBuilder();
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
                childTextSb.Append(cText).Append(' ');
            }
            return true;
        }, IntPtr.Zero);

        string win32Text = title + " " + childTextSb.ToString();

        // Extract UI Automation texts (essential for DirectUIHWND where WM_GETTEXT returns empty)
        string uiaText = string.Empty;
        if (isDialogClass || isExplorer)
        {
            uiaText = ExtractAllWindowTextViaUia(hWnd);
        }

        string allText = win32Text + " " + uiaText;

        bool isPermissionOrDeniedDialog =
            allText.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
            allText.Contains("access", StringComparison.OrdinalIgnoreCase) ||
            allText.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
            allText.Contains("not accessible", StringComparison.OrdinalIgnoreCase) ||
            allText.Contains("permanently", StringComparison.OrdinalIgnoreCase) ||
            allText.Contains("administrator", StringComparison.OrdinalIgnoreCase) ||
            allText.Contains("Location is not available", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Location is not available", StringComparison.OrdinalIgnoreCase);

        // Active selection in Windows Explorer (if user just double-clicked a folder)
        string? activeSelectedPath = null;
        if (isPermissionOrDeniedDialog || isExplorer || isDialogClass)
        {
            activeSelectedPath = GetActiveExplorerSelectedPath();
        }

        foreach (var folder in lockedFolders)
        {
            string fullPath = folder.FolderPath.TrimEnd('\\', '/');
            string folderName = folder.FolderName;
            if (string.IsNullOrEmpty(folderName))
            {
                folderName = Path.GetFileName(fullPath);
            }

            // 1. Direct path match anywhere in dialog text / UI Automation
            bool pathMatch = allText.Contains(fullPath, StringComparison.OrdinalIgnoreCase);

            // 2. Active selection match in Explorer
            bool selectionMatch = !string.IsNullOrEmpty(activeSelectedPath) &&
                                  (activeSelectedPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase) ||
                                   activeSelectedPath.TrimEnd('\\', '/').Equals(fullPath, StringComparison.OrdinalIgnoreCase));

            // 3. Folder name match combined with error dialog
            bool nameMatch = !string.IsNullOrEmpty(folderName) &&
                             allText.Contains(folderName, StringComparison.OrdinalIgnoreCase) &&
                             (isPermissionOrDeniedDialog || isExplorer);

            // 4. Window title match
            bool titleMatch = string.Equals(title, folderName, StringComparison.OrdinalIgnoreCase) ||
                              title.Contains(fullPath, StringComparison.OrdinalIgnoreCase);

            // 5. Fallback for single locked folder: if any location/access denied dialog from Explorer appears
            bool singleFolderFallback = lockedFolders.Count == 1 &&
                                        (isPermissionOrDeniedDialog || title.Contains("Location", StringComparison.OrdinalIgnoreCase)) &&
                                        (isExplorer || isDialogClass);

            if (pathMatch || selectionMatch || nameMatch || titleMatch || singleFolderFallback)
            {
                // Debounce repeated triggers for same folder within 2 seconds
                if (DateTime.UtcNow - _lastPopupTime < TimeSpan.FromSeconds(2) && _lastHandledFolder == fullPath)
                {
                    DismissDialog(hWnd);
                    return;
                }

                _lastPopupTime = DateTime.UtcNow;
                _lastHandledFolder = fullPath;

                // 1. Instantly hide and dismiss the Windows Explorer error dialog
                DismissDialog(hWnd);

                // 2. Dispatch SecApper unlock popup
                _dispatchToUi(() =>
                {
                    FolderUnlockRequested?.Invoke(folder);
                });

                return;
            }
        }
    }

    private static string ExtractAllWindowTextViaUia(IntPtr hWnd)
    {
        var sb = new StringBuilder();
        try
        {
            var element = AutomationElement.FromHandle(hWnd);
            if (element == null) return string.Empty;

            try
            {
                string rootName = element.Current.Name;
                if (!string.IsNullOrEmpty(rootName)) sb.Append(rootName).Append(' ');
            }
            catch { }

            try
            {
                var children = element.FindAll(TreeScope.Subtree, Condition.TrueCondition);
                foreach (AutomationElement child in children)
                {
                    try
                    {
                        string name = child.Current.Name;
                        if (!string.IsNullOrEmpty(name))
                        {
                            sb.Append(name).Append(' ');
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
        catch { }

        return sb.ToString();
    }

    private static string? GetActiveExplorerSelectedPath()
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return null;

            object? shell = Activator.CreateInstance(shellType);
            if (shell == null) return null;

            try
            {
                dynamic dynShell = shell;
                dynamic? windows = dynShell.Windows();
                if (windows == null) return null;

                int count = 0;
                try { count = (int)windows.Count; } catch { return null; }

                for (int i = 0; i < count; i++)
                {
                    object? windowItem = null;
                    try
                    {
                        windowItem = windows.Item(i);
                        if (windowItem == null) continue;

                        dynamic dynItem = windowItem;
                        dynamic? doc = dynItem.Document;
                        if (doc == null) continue;

                        object? selObj = doc.SelectedItems();
                        if (selObj != null)
                        {
                            dynamic selected = selObj;
                            int selCount = (int)selected.Count;
                            for (int s = 0; s < selCount; s++)
                            {
                                dynamic? selItem = selected.Item(s);
                                string? p = selItem?.Path;
                                if (!string.IsNullOrEmpty(p)) return p;
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        if (windowItem != null && Marshal.IsComObject(windowItem))
                        {
                            try { Marshal.ReleaseComObject(windowItem); } catch { }
                        }
                    }
                }
            }
            finally
            {
                if (shell != null && Marshal.IsComObject(shell))
                {
                    try { Marshal.ReleaseComObject(shell); } catch { }
                }
            }
        }
        catch { }
        return null;
    }

    private static void DismissDialog(IntPtr hWnd)
    {
        try
        {
            // 1. Immediately hide window from user's display
            ShowWindow(hWnd, SW_HIDE);

            // 2. Click OK or Cancel button via UI Automation InvokePattern
            try
            {
                var element = AutomationElement.FromHandle(hWnd);
                if (element != null)
                {
                    var buttons = element.FindAll(TreeScope.Subtree, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                    foreach (AutomationElement btn in buttons)
                    {
                        try
                        {
                            if (btn.TryGetCurrentPattern(InvokePattern.Pattern, out object patternObj) && patternObj is InvokePattern invoker)
                            {
                                invoker.Invoke();
                                break;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            // 3. Post Win32 messages to guarantee dismissal
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

            PostMessage(hWnd, WM_COMMAND, (IntPtr)1 /*IDOK*/, IntPtr.Zero);
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
    private const int GWL_STYLE = -16;
    private const int WS_POPUP = unchecked((int)0x80000000);

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

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    private static int GetWindowLong(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
            return (int)GetWindowLongPtr64(hWnd, nIndex).ToInt64();
        return GetWindowLong32(hWnd, nIndex);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder text, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
