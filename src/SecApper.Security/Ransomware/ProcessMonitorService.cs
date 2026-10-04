using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SecApper.Security.Ransomware;

public class ProcessMonitorService : IProcessMonitorService
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public (string? ProcessName, int? ProcessId, string? ProcessPath) GetCurrentActiveProcessContext()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return (null, null, null);

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return (null, null, null);

            using var process = Process.GetProcessById((int)pid);
            string? name = process.ProcessName;
            string? path = null;
            try
            {
                path = process.MainModule?.FileName;
            }
            catch
            {
                // Access to process module path may be restricted for system processes
            }

            return (name, (int)pid, path);
        }
        catch
        {
            return (null, null, null);
        }
    }
}
