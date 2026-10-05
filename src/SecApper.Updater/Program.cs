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
        Log($"Target Directory: {targetDir}");
        Log($"Package: {packagePath}");

        // Step 1: Wait for caller application to terminate
        if (callerPid > 0)
        {
            Log($"Waiting for caller process {callerPid} to exit...");
            try
            {
                var proc = Process.GetProcessById(callerPid);
                if (!proc.WaitForExit(15000))
                {
                    Log($"Caller PID {callerPid} did not exit within 15 seconds, killing...");
                    proc.Kill();
                    proc.WaitForExit(3000);
                }
            }
            catch (ArgumentException)
            {
                // Already exited
                Log("Caller process already exited.");
            }
            catch (Exception ex)
            {
                Log($"Warning waiting for caller: {ex.Message}");
            }
        }

        // Give file handles 1 second to release completely
        Thread.Sleep(1000);

        // Step 2: Create atomic backup directory of existing application files
        string backupDir = Path.Combine(targetDir, $"_backup_{DateTime.UtcNow:yyyyMMddHHmmss}");
        bool backupCreated = false;

        try
        {
            Directory.CreateDirectory(backupDir);
            foreach (var file in Directory.GetFiles(targetDir))
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
                    string destPath = Path.Combine(targetDir, entry.FullName);
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
                var psi = new ProcessStartInfo
                {
                    FileName = packagePath,
                    Arguments = $"/SILENT /SP- /NORESTART /CLOSEAPPLICATIONS /DIR=\"{targetDir}\"",
                    UseShellExecute = true
                };

                using var installerProc = Process.Start(psi);
                if (installerProc != null)
                {
                    installerProc.WaitForExit(120000);
                    if (installerProc.ExitCode == 0)
                    {
                        updateApplied = true;
                        Log("Installer completed successfully.");
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
        string mainExe = Path.Combine(targetDir, exeName);
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
                        File.Copy(file, Path.Combine(targetDir, fName), true);
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
        }

        // Step 5: Launch the application
        if (File.Exists(mainExe))
        {
            try
            {
                Log($"Relaunching application: {mainExe}");
                var psi = new ProcessStartInfo
                {
                    FileName = mainExe,
                    WorkingDirectory = targetDir,
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
        return 0;
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
