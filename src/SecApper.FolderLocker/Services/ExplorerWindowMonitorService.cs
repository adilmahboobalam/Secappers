using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SecApper.FolderLocker.Services;

public interface IExplorerWindowMonitorService : IDisposable
{
    void Start();
    void Stop();
    void TrackFolderWindow(string folderId, string folderPath);
    void UntrackFolder(string folderId);
    event Action<string /*folderId*/, string /*folderPath*/>? FolderWindowClosed;
}

/// <summary>
/// Monitors open Windows Explorer windows/tabs for unlocked folders.
/// When the user closes the Explorer window (or navigates away),
/// this service automatically fires FolderWindowClosed to trigger re-locking.
/// </summary>
public class ExplorerWindowMonitorService : IExplorerWindowMonitorService
{
    private readonly Action<Action> _dispatchToUi;
    private readonly ConcurrentDictionary<string, TrackedFolderSession> _trackedSessions = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;

    public event Action<string, string>? FolderWindowClosed;

    public ExplorerWindowMonitorService(Action<Action> dispatchToUi)
    {
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
        _trackedSessions.Clear();
    }

    public void TrackFolderWindow(string folderId, string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderId) || string.IsNullOrWhiteSpace(folderPath))
            return;

        string normalized = Path.GetFullPath(folderPath).TrimEnd('\\');
        string name = Path.GetFileName(normalized);
        if (string.IsNullOrEmpty(name)) name = normalized;

        var session = new TrackedFolderSession
        {
            FolderId = folderId,
            FolderPath = folderPath,
            NormalizedPath = normalized,
            FolderName = name,
            StartedAt = DateTime.UtcNow,
            WindowHasOpened = false,
            ConsecutiveClosedChecks = 0
        };

        _trackedSessions[folderId] = session;
    }

    public void UntrackFolder(string folderId)
    {
        if (string.IsNullOrWhiteSpace(folderId)) return;
        _trackedSessions.TryRemove(folderId, out _);
    }

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(500, ct);

                if (_trackedSessions.IsEmpty) continue;

                var sessions = _trackedSessions.Values.ToList();
                foreach (var session in sessions)
                {
                    bool isOpen = IsFolderWindowCurrentlyOpen(session);

                    if (isOpen)
                    {
                        session.WindowHasOpened = true;
                        session.ConsecutiveClosedChecks = 0;
                    }
                    else
                    {
                        if (session.WindowHasOpened)
                        {
                            session.ConsecutiveClosedChecks++;
                            // Require 2 consecutive checks (~1000ms) to ensure window is truly closed,
                            // avoiding false positives during folder refresh (F5) or tab changes.
                            if (session.ConsecutiveClosedChecks >= 2)
                            {
                                _trackedSessions.TryRemove(session.FolderId, out _);

                                _dispatchToUi(() =>
                                {
                                    FolderWindowClosed?.Invoke(session.FolderId, session.FolderPath);
                                });
                            }
                        }
                        else
                        {
                            // Window has not been detected open yet.
                            // Allow up to 15 seconds for Explorer to launch and render the window.
                            if (DateTime.UtcNow - session.StartedAt > TimeSpan.FromSeconds(15))
                            {
                                // User closed it immediately before detection or launch failed
                                _trackedSessions.TryRemove(session.FolderId, out _);

                                _dispatchToUi(() =>
                                {
                                    FolderWindowClosed?.Invoke(session.FolderId, session.FolderPath);
                                });
                            }
                        }
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

    private bool IsFolderWindowCurrentlyOpen(TrackedFolderSession session)
    {
        var foundHwnds = new HashSet<IntPtr>();

        // 1. Check COM IShellWindows (primary: exact path matching and Windows 11 tabs support)
        if (CheckComShellWindows(session.NormalizedPath, foundHwnds))
        {
            foreach (var h in foundHwnds) session.KnownWindowHandles.Add(h);
            return true;
        }

        // 2. Check Win32 CabinetWClass (fallback: checks top-level Explorer windows by title/class)
        if (CheckWin32CabinetWindows(session.NormalizedPath, session.FolderName, foundHwnds))
        {
            foreach (var h in foundHwnds) session.KnownWindowHandles.Add(h);
            return true;
        }

        return false;
    }

    private static bool CheckComShellWindows(string targetNormalizedPath, HashSet<IntPtr> foundHwnds)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return false;

            object? shell = Activator.CreateInstance(shellType);
            if (shell == null) return false;

            try
            {
                dynamic dynShell = shell;
                dynamic? windows = dynShell.Windows();
                if (windows == null) return false;

                int count = 0;
                try { count = (int)windows.Count; } catch { return false; }

                for (int i = 0; i < count; i++)
                {
                    object? windowItem = null;
                    try
                    {
                        windowItem = windows.Item(i);
                        if (windowItem == null) continue;

                        dynamic dynItem = windowItem;

                        IntPtr hwnd = IntPtr.Zero;
                        try
                        {
                            long h = (long)dynItem.HWND;
                            hwnd = new IntPtr(h);
                        }
                        catch { }

                        // 1. Match LocationURL (e.g. file:///C:/Path/To/Folder)
                        string? url = null;
                        try { url = dynItem.LocationURL; } catch { }

                        if (!string.IsNullOrEmpty(url) && url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
                            {
                                string local = Path.GetFullPath(uri.LocalPath).TrimEnd('\\');
                                if (string.Equals(local, targetNormalizedPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (hwnd != IntPtr.Zero) foundHwnds.Add(hwnd);
                                    return true;
                                }
                            }
                        }

                        // 2. Match Document.Folder.Self.Path
                        try
                        {
                            dynamic? doc = dynItem.Document;
                            string? docPath = doc?.Folder?.Self?.Path;
                            if (!string.IsNullOrEmpty(docPath))
                            {
                                string local = Path.GetFullPath(docPath).TrimEnd('\\');
                                if (string.Equals(local, targetNormalizedPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (hwnd != IntPtr.Zero) foundHwnds.Add(hwnd);
                                    return true;
                                }
                            }
                        }
                        catch { }
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
                if (Marshal.IsComObject(shell))
                {
                    try { Marshal.ReleaseComObject(shell); } catch { }
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool CheckWin32CabinetWindows(string targetNormalizedPath, string folderName, HashSet<IntPtr> foundHwnds)
    {
        bool found = false;
        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            var sbClass = new StringBuilder(256);
            GetClassName(hWnd, sbClass, sbClass.Capacity);
            string className = sbClass.ToString();

            if (className == "CabinetWClass" || className == "ExploreWClass")
            {
                var sbTitle = new StringBuilder(512);
                GetWindowText(hWnd, sbTitle, sbTitle.Capacity);
                string title = sbTitle.ToString().Trim();

                if (!string.IsNullOrEmpty(title))
                {
                    if (string.Equals(title, folderName, StringComparison.OrdinalIgnoreCase) ||
                        title.IndexOf(targetNormalizedPath, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        foundHwnds.Add(hWnd);
                        found = true;
                        return false; // Stop enumeration
                    }
                }
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }

    public void Dispose()
    {
        Stop();
    }

    private class TrackedFolderSession
    {
        public string FolderId { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public string NormalizedPath { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public bool WindowHasOpened { get; set; }
        public int ConsecutiveClosedChecks { get; set; }
        public HashSet<IntPtr> KnownWindowHandles { get; } = new();
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);
}
