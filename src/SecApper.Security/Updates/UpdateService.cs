using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SecApper.Security.Data;

namespace SecApper.Security.Updates;

public class UpdateService : IUpdateService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly IDatabaseService _databaseService;
    private readonly string _currentVersion;

    public string CurrentVersion => _currentVersion;

    public UpdateService(IDatabaseService databaseService, string? customVersion = null)
    {
        _databaseService = databaseService;
        _currentVersion = customVersion ?? ResolveCurrentVersion();
    }

    private static string ResolveCurrentVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var ver = asm.GetName().Version;
        return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(bool isManualCheck = false, CancellationToken ct = default)
    {
        try
        {
            // 1. Check if auto updates are enabled
            if (!isManualCheck)
            {
                string? autoCheck = await _databaseService.GetSettingAsync("AutoCheckUpdates", "true");
                if (string.Equals(autoCheck, "false", StringComparison.OrdinalIgnoreCase))
                {
                    return new UpdateCheckResult(false, null, _currentVersion, "Automatic update checks are disabled in settings.");
                }

                string? lastCheckStr = await _databaseService.GetSettingAsync("LastUpdateCheckTime");
                string? freq = await _databaseService.GetSettingAsync("UpdateCheckFrequency", "Daily");

                if (!string.IsNullOrEmpty(lastCheckStr) && DateTime.TryParse(lastCheckStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastCheck))
                {
                    var interval = freq switch
                    {
                        "Weekly" => TimeSpan.FromDays(7),
                        "Startup" => TimeSpan.FromMinutes(1),
                        _ => TimeSpan.FromDays(1)
                    };

                    if (DateTime.UtcNow - lastCheck < interval)
                    {
                        return new UpdateCheckResult(false, null, _currentVersion, null); // Up to date / throttle
                    }
                }
            }

            // 2. Resolve manifest URL
            string? manifestUrl = await _databaseService.GetSettingAsync("UpdateManifestUrl");
            if (string.IsNullOrWhiteSpace(manifestUrl) || manifestUrl.Contains("updates.secapper.com", StringComparison.OrdinalIgnoreCase))
            {
                string localManifest = @"C:\Users\Adi\Desktop\Secapper\latest.json";
                string programDataManifest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SecApper", "FolderLocker", "updates", "latest.json");
                if (File.Exists(localManifest)) manifestUrl = new Uri(localManifest).AbsoluteUri;
                else if (File.Exists(programDataManifest)) manifestUrl = new Uri(programDataManifest).AbsoluteUri;
                else manifestUrl = "https://updates.secapper.com/secapper/stable/latest.json";
            }

            string json;
            if (manifestUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase) || File.Exists(manifestUrl))
            {
                string filePath = manifestUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase) 
                    ? new Uri(manifestUrl).LocalPath 
                    : manifestUrl;

                if (!File.Exists(filePath))
                {
                    return new UpdateCheckResult(false, null, _currentVersion, $"Local update manifest file not found: {filePath}");
                }

                json = await File.ReadAllTextAsync(filePath, ct);
            }
            else
            {
                // Enforce HTTPS or loopback for testing
                if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback))
                {
                    return new UpdateCheckResult(false, null, _currentVersion, "Security error: Remote update manifest URL must use HTTPS.");
                }

                try
                {
                    using var response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseContentRead, ct);
                    response.EnsureSuccessStatusCode();
                    json = await response.Content.ReadAsStringAsync(ct);
                }
                catch (HttpRequestException ex)
                {
                    if (uri.Host.Contains("secapper.com", StringComparison.OrdinalIgnoreCase))
                    {
                        return new UpdateCheckResult(false, null, _currentVersion, 
                            "The default update server (updates.secapper.com) is currently offline.\n\n" +
                            "To test live updates:\n" +
                            "• Set your custom update server URL in Settings → Updates\n" +
                            "• Or click '🚀 Simulate Live Update' in Settings to test the full live download and install workflow!");
                    }

                    return new UpdateCheckResult(false, null, _currentVersion, $"Update check failed: Internet connection or update server unavailable ({ex.Message}).");
                }
            }

            var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (updateInfo == null || string.IsNullOrWhiteSpace(updateInfo.Version) || string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
            {
                return new UpdateCheckResult(false, null, _currentVersion, "Invalid update manifest format.");
            }

            // 4. Record last check time
            await _databaseService.SetSettingAsync("LastUpdateCheckTime", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

            // 5. Compare semantic versions
            if (!SemVersion.TryParse(_currentVersion, out var currentSemVer) ||
                !SemVersion.TryParse(updateInfo.Version, out var availableSemVer))
            {
                return new UpdateCheckResult(false, null, _currentVersion, "Could not parse semantic versions.");
            }

            bool isNewer = availableSemVer > currentSemVer;
            return new UpdateCheckResult(isNewer, isNewer ? updateInfo : null, _currentVersion, null);
        }
        catch (HttpRequestException ex)
        {
            // Offline or server unreachable
            return new UpdateCheckResult(false, null, _currentVersion, $"Update check failed: Internet connection or update server unavailable ({ex.Message}).");
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, null, _currentVersion, $"Update check error: {ex.Message}");
        }
    }

    public async Task<string> DownloadUpdateAsync(UpdateInfo update, IProgress<UpdateProgress>? progress = null, CancellationToken ct = default)
    {
        if (update == null || string.IsNullOrWhiteSpace(update.DownloadUrl))
            throw new ArgumentException("Download URL cannot be empty.", nameof(update));

        if (!Uri.TryCreate(update.DownloadUrl, UriKind.Absolute, out var uri) || 
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeFile && !uri.IsLoopback))
        {
            throw new InvalidOperationException("Security violation: Updates may only be downloaded over HTTPS or local test channels.");
        }

        string updatesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SecApper", "FolderLocker", "updates");
        Directory.CreateDirectory(updatesDir);

        string ext = Path.GetExtension(uri.LocalPath);
        if (string.IsNullOrEmpty(ext)) ext = ".exe";
        string targetFile = Path.Combine(updatesDir, $"SecApperUpdate_{update.Version}{ext}");

        progress?.Report(new UpdateProgress(0, 0, "Connecting to update source..."));

        if (uri.Scheme == Uri.UriSchemeFile || File.Exists(update.DownloadUrl))
        {
            string sourcePath = uri.Scheme == Uri.UriSchemeFile ? uri.LocalPath : update.DownloadUrl;
            var fileInfo = new FileInfo(sourcePath);
            long totalBytes = fileInfo.Length;

            await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var destStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            byte[] buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;
                progress?.Report(new UpdateProgress(totalRead, totalBytes, $"Copying update package ({totalRead / (1024 * 1024)} MB / {totalBytes / (1024 * 1024)} MB)..."));
                await Task.Delay(15, ct); // Smooth progress animation
            }

            progress?.Report(new UpdateProgress(totalRead, totalBytes, "Download completed. Verifying package..."));
            return targetFile;
        }
        else
        {
            using var response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1;

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            byte[] buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;
                progress?.Report(new UpdateProgress(totalRead, totalBytes, $"Downloading update ({totalRead / (1024 * 1024)} MB)..."));
            }

            progress?.Report(new UpdateProgress(totalRead, totalBytes, "Download completed. Verifying package..."));
            return targetFile;
        }
    }

    public async Task<UpdateInfo> GetSimulatedUpdateInfoAsync()
    {
        await Task.Yield();
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string setupExe = Path.Combine(baseDir, "SecApperFolderLockerSetup.exe");
        if (!File.Exists(setupExe))
        {
            setupExe = Path.Combine(baseDir, "..", "..", "..", "..", "..", "installer", "output", "SecApperFolderLockerSetup.exe");
            if (File.Exists(setupExe)) setupExe = Path.GetFullPath(setupExe);
        }

        string sha256 = "0000000000000000000000000000000000000000000000000000000000000000";
        if (File.Exists(setupExe))
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(setupExe);
            byte[] hash = await sha.ComputeHashAsync(fs);
            sha256 = Convert.ToHexString(hash).ToLowerInvariant();
        }

        return new UpdateInfo
        {
            Version = "1.1.0",
            ReleaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            DownloadUrl = File.Exists(setupExe) ? new Uri(setupExe).AbsoluteUri : "https://updates.secapper.com/secapper/1.1.0/SecApperFolderLockerSetup.exe",
            Sha256 = sha256,
            Mandatory = false,
            MinSupportedVersion = "1.0.0",
            ReleaseNotes = "SecApper v1.1.0 Major Update:\n• Active double-click Windows Explorer unlock popup\n• Native NTFS Discretionary Access Control enforcement\n• Operation journal crash recovery\n• Background ransomware threat protection\n• Standalone zero-downtime auto-updater"
        };
    }

    public bool VerifyUpdatePackage(string filePath, UpdateInfo expectedUpdate, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            errorMessage = "Update package file does not exist on disk.";
            return false;
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            errorMessage = "Update package file is empty.";
            return false;
        }

        // 1. Verify SHA-256 Checksum
        if (!string.IsNullOrWhiteSpace(expectedUpdate.Sha256))
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            string computedHex = Convert.ToHexString(hash);

            if (!string.Equals(computedHex, expectedUpdate.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"Integrity verification failed! Checksum mismatch.\nExpected: {expectedUpdate.Sha256}\nComputed: {computedHex}";
                return false;
            }
        }

        // 2. Validate version compatibility
        if (!string.IsNullOrWhiteSpace(expectedUpdate.MinSupportedVersion))
        {
            if (SemVersion.TryParse(expectedUpdate.MinSupportedVersion, out var minVer) &&
                SemVersion.TryParse(_currentVersion, out var currVer) &&
                currVer < minVer)
            {
                errorMessage = $"Update requires at least version {expectedUpdate.MinSupportedVersion}. Installed version is {_currentVersion}.";
                return false;
            }
        }

        return true;
    }

    public async Task<bool> LaunchUpdaterAndExitAsync(string packagePath, UpdateInfo update)
    {
        await Task.Yield();
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string updaterExe = Path.Combine(baseDir, "SecApper.Updater.exe");

        // If updater is not alongside main exe, look in parent/publish directory
        if (!File.Exists(updaterExe))
        {
            string altPath = Path.Combine(baseDir, "..", "SecApper.Updater", "bin", "Debug", "net8.0-windows", "SecApper.Updater.exe");
            if (File.Exists(altPath)) updaterExe = Path.GetFullPath(altPath);
        }

        int currentPid = Environment.ProcessId;
        string exeName = "SecApper.FolderLocker.exe";

        if (File.Exists(updaterExe))
        {
            var psi = new ProcessStartInfo
            {
                FileName = updaterExe,
                Arguments = $"--caller-pid {currentPid} --package \"{packagePath}\" --target-dir \"{baseDir}\" --executable \"{exeName}\" --version \"{update.Version}\"",
                WorkingDirectory = baseDir,
                UseShellExecute = true
            };

            Process.Start(psi);
            return true;
        }
        else
        {
            // If installer package is an Inno Setup installer (.exe), execute installer directly with silent flags
            if (string.Equals(Path.GetExtension(packagePath), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = packagePath,
                    Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                    UseShellExecute = true
                };

                Process.Start(psi);
                return true;
            }

            return false;
        }
    }
}
