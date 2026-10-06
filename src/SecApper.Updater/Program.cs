using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace SecApper.Updater;

public static class Program
{
    private static string _logFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SecApper", "FolderLocker", "logs", "updater.log");

    public static int Main(string[] args)
    {
        Log("=== SecApper Updater Started ===");

        int callerPid = 0;
        string? packagePath = null;
        string? targetDir = null;
        string exeName = "SecApper.FolderLocker.exe";
        string? version = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--caller-pid" && i + 1 < args.Length)
                int.TryParse(args[++i], out callerPid);
            else if (args[i] == "--package" && i + 1 < args.Length)
                packagePath = args[++i];
            else if (args[i] == "--target-dir" && i + 1 < args.Length)
                targetDir = args[++i];
            else if (args[i] == "--executable" && i + 1 < args.Length)
                exeName = args[++i];
            else if (args[i] == "--version" && i + 1 < args.Length)
                version = args[++i];
        }

        if (string.IsNullOrEmpty(packagePath) || !File.Exists(packagePath))
        {
            Log($"Error: Update package not found: '{packagePath}'");
            return 1;
        }

        if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
        {
            targetDir = AppDomain.CurrentDomain.BaseDirectory;
        }

        targetDir = Path.GetFullPath(targetDir);
        string cleanTargetDir = targetDir.TrimEnd('\\');

        Log($"Target Directory: {cleanTargetDir}");
        Log($"Package: {packagePath}");

        // Step 1: Forcefully terminate ALL running SecApper instances to guarantee zero file locks
        Log("Terminating all active SecApper instances to ensure zero file locks...");

        // A. Terminate specific caller process if provided
        if (callerPid > 0)
        {
            try
            {
                var callerProc = Process.GetProcessById(callerPid);
                if (!callerProc.HasExited)
                {
                    Log($"Terminating caller process PID {callerPid}...");
                    try { callerProc.Kill(); } catch { }
                    callerProc.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                Log($"Caller PID {callerPid} already exited or inaccessible: {ex.Message}");
            }
        }

        // B. Terminate all SecApper.FolderLocker processes via standard Process.Kill()
        try
        {
            var runningProcs = Process.GetProcessesByName("SecApper.FolderLocker");
            foreach (var proc in runningProcs)
            {
                try
                {
                    Log($"Terminating SecApper process PID {proc.Id}...");
                    proc.Kill();
                    proc.WaitForExit(3000);
                }
                catch (Exception pEx)
                {
                    Log($"Warning terminating PID {proc.Id}: {pEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Warning finding SecApper processes: {ex.Message}");
        }

        // C. Fallback: Run taskkill to ensure no lingering child processes keep DLLs locked
        try
        {
            using var tk = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill.exe",
                Arguments = "/F /IM SecApper.FolderLocker.exe",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            tk?.WaitForExit(3000);
        }
        catch { }

        // D. Terminate any in-folder process
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id != Environment.ProcessId &&
                        p.MainModule?.FileName != null &&
                        p.MainModule.FileName.StartsWith(cleanTargetDir, StringComparison.OrdinalIgnoreCase))
                    {
                        Log($"Terminating in-folder process {p.ProcessName} (PID {p.Id})...");
                        p.Kill();
                        p.WaitForExit(2000);
                    }
                }
                catch { }
            }
        }
        catch { }

        // E. Wait until all SecApper processes have completely exited
        int waitAttempts = 0;
        while (waitAttempts < 15)
        {
            var procs = Process.GetProcessesByName("SecApper.FolderLocker");
            if (procs.Length == 0) break;
            Thread.Sleep(500);
            waitAttempts++;
        }

        // F. Verify target executable and runtime DLLs are unlocked and writable
        string testExe = Path.Combine(cleanTargetDir, exeName);
        if (File.Exists(testExe))
        {
            int lockCheck = 0;
            while (lockCheck < 10)
            {
                try
                {
                    using var fs = File.Open(testExe, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    Log("Target executable verified unlocked and writable.");
                    break;
                }
                catch
                {
                    lockCheck++;
                    Log($"Waiting for file lock on {testExe} to release ({lockCheck}/10)...");
                    Thread.Sleep(500);
                }
            }
        }

        string testClrJit = Path.Combine(cleanTargetDir, "clrjit.dll");
        if (File.Exists(testClrJit))
        {
            int lockCheck = 0;
            while (lockCheck < 10)
            {
                try
                {
                    using var fs = File.Open(testClrJit, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    Log("Runtime clrjit.dll verified unlocked and writable.");
                    break;
                }
                catch
                {
                    lockCheck++;
                    Log($"Waiting for file lock on {testClrJit} to release ({lockCheck}/10)...");
                    Thread.Sleep(500);
                }
            }
        }

        // Step 2: Create atomic backup directory of existing application files
        string backupDir = Path.Combine(cleanTargetDir, $"_backup_{DateTime.UtcNow:yyyyMMddHHmmss}");
        bool backupCreated = false;

        try
        {
            Directory.CreateDirectory(backupDir);
            foreach (var file in Directory.GetFiles(cleanTargetDir))
            {
                string fName = Path.GetFileName(file);
                // Do not copy updater logs or previous backups
                if (!fName.StartsWith("_backup_") && !fName.EndsWith(".log"))
                {
                    File.Copy(file, Path.Combine(backupDir, fName), true);
                }
            }
            backupCreated = true;
            Log($"Created backup of current files at: {backupDir}");
        }
        catch (Exception ex)
        {
            Log($"Warning: Failed to create full backup: {ex.Message}");
        }

        // Step 3: Apply the update package
        bool updateApplied = false;
        try
        {
            string ext = Path.GetExtension(packagePath).ToLowerInvariant();

            if (ext == ".zip")
            {
                Log("Extracting ZIP update package...");
                using var archive = ZipFile.OpenRead(packagePath);
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue; // Directory entry
                    string destPath = Path.Combine(cleanTargetDir, entry.FullName);
                    string? destFileDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destFileDir) && !Directory.Exists(destFileDir))
                    {
                        Directory.CreateDirectory(destFileDir);
                    }
                    entry.ExtractToFile(destPath, true);
                }
                updateApplied = true;
                Log("ZIP extraction complete.");
            }
            else if (ext == ".exe")
            {
                Log("Running installer in silent update mode...");
                string innoLog = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SecApper", "FolderLocker", "logs", "installer_exec.log");

                var psi = new ProcessStartInfo
                {
                    FileName = packagePath,
                    Arguments = $"/VERYSILENT /SUPPRESSMSGBOXES /SP- /NORESTART /FORCECLOSEAPPLICATIONS /DIR=\"{cleanTargetDir}\" /LOG=\"{innoLog}\"",
                    UseShellExecute = true
                };

                using var installerProc = Process.Start(psi);
                if (installerProc != null)
                {
                    installerProc.WaitForExit(180000);
                    if (installerProc.ExitCode == 0)
                    {
                        updateApplied = true;
                        Log("Installer completed successfully (exit code 0).");
                    }
                    else
                    {
                        Log($"Installer failed with exit code: {installerProc.ExitCode}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error applying update: {ex.Message}");
            updateApplied = false;
        }

        // Step 4: Verify target executable exists
        string mainExe = Path.Combine(cleanTargetDir, exeName);
        if (!updateApplied || !File.Exists(mainExe))
        {
            Log("Update failed or main executable missing! Executing rollback...");
            if (backupCreated && Directory.Exists(backupDir))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(backupDir))
                    {
                        string fName = Path.GetFileName(file);
                        File.Copy(file, Path.Combine(cleanTargetDir, fName), true);
                    }
                    Log("Rollback successful. Previous version restored.");
                }
                catch (Exception rbEx)
                {
                    Log($"Fatal: Rollback failed: {rbEx.Message}");
                }
            }
        }
        else
        {
            Log($"Update applied successfully to version {version ?? "latest"}.");
            try
            {
                if (backupCreated && Directory.Exists(backupDir))
                {
                    Directory.Delete(backupDir, true);
                    Log("Temporary update backup cleaned up.");
                }
            }
            catch { }
        }

        // Step 5: Launch the updated application
        if (File.Exists(mainExe))
        {
            try
            {
                Log($"Relaunching application: {mainExe}");
                var psi = new ProcessStartInfo
                {
                    FileName = mainExe,
                    WorkingDirectory = cleanTargetDir,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Log($"Failed to launch application after update: {ex.Message}");
            }
        }

        Log("=== SecApper Updater Completed ===");
        return updateApplied ? 0 : 1;
    }

    private static void Log(string message)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_logFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}] {message}\r\n";
            File.AppendAllText(_logFile, line);
            Console.WriteLine(line);
        }
        catch { }
    }
}
