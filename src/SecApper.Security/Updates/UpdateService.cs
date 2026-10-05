using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SecApper.Security.Data;

namespace SecApper.Security.Updates;

public class UpdateService : IUpdateService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SecApper-Updater/1.1.0 (Windows NT 10.0; Win64; x64)");
        return client;
    }

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

            // 2. Resolve and normalize manifest URL
            string? storedManifest = await _databaseService.GetSettingAsync("UpdateManifestUrl");
            string manifestUrl = NormalizeManifestUrl(storedManifest);

            // If the database stored a raw web URL or outdated endpoint, auto-heal it
            if (!string.Equals(storedManifest, manifestUrl, StringComparison.OrdinalIgnoreCase))
            {
                await _databaseService.SetSettingAsync("UpdateManifestUrl", manifestUrl);
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
                string requestUrl = manifestUrl;
                if (requestUrl.Contains("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
                {
                    string sep = requestUrl.Contains('?') ? "&" : "?";
                    requestUrl = $"{requestUrl}{sep}_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                }

                // Enforce HTTPS or loopback for testing
                if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback))
                {
                    return new UpdateCheckResult(false, null, _currentVersion, "Security error: Remote update manifest URL must use HTTPS.");
                }

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                    request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true, NoStore = true };

                    using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound && uri.Host.Contains("github", StringComparison.OrdinalIgnoreCase))
                    {
                        return new UpdateCheckResult(false, null, _currentVersion,
                            "Remote manifest not found on GitHub (404):\n\n" +
                            "If your GitHub repository 'adilmahboobalam/Secappers' is set to Private, please change repository visibility to Public in GitHub Settings (or upload latest.json and setup exe to a public GitHub Release) so SecApper clients can query and install live updates.");
                    }
                    response.EnsureSuccessStatusCode();
                    json = await response.Content.ReadAsStringAsync(ct);
                }
                catch (HttpRequestException ex)
                {
                    return new UpdateCheckResult(false, null, _currentVersion, $"Update check failed: Internet connection or update server unavailable ({ex.Message}).");
                }
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return new UpdateCheckResult(false, null, _currentVersion, "Update check failed: Empty response received from update manifest server.");
            }

            // Detect if a webpage or HTML error was returned instead of raw JSON
            if (json.TrimStart().StartsWith("<", StringComparison.Ordinal))
            {
                return new UpdateCheckResult(false, null, _currentVersion,
                    "Update check error: The server returned an HTML webpage instead of a JSON manifest.\n\n" +
                    $"Manifest Endpoint: {manifestUrl}\n\n" +
                    "Please ensure the Update Manifest URL points directly to raw JSON (e.g. https://raw.githubusercontent.com/adilmahboobalam/Secappers/main/latest.json).");
            }

            UpdateInfo? updateInfo;
            try
            {
                updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                return new UpdateCheckResult(false, null, _currentVersion,
                    $"Update check error: The update manifest is not valid JSON ({ex.Message}).");
            }

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
            return new UpdateCheckResult(isNewer, updateInfo, _currentVersion, null);
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
            HttpResponseMessage response;
            try
            {
                response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound && 
                    uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase) && 
                    uri.AbsolutePath.Contains("/releases/download/"))
                {
                    // Fallback to raw repository path
                    string fileName = Path.GetFileName(uri.LocalPath);
                    string rawFallback = $"https://github.com/adilmahboobalam/Secappers/raw/main/installer/output/{fileName}";
                    if (Uri.TryCreate(rawFallback, UriKind.Absolute, out var fallbackUri))
                    {
                        response.Dispose();
                        response = await HttpClient.GetAsync(fallbackUri, HttpCompletionOption.ResponseHeadersRead, ct);
                    }
                }
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException($"Could not download update package from server: {ex.Message}", ex);
            }

            long totalBytes = -1;
            long totalRead = 0;

            using (response)
            {
                totalBytes = response.Content.Headers.ContentLength ?? -1;

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                byte[] buffer = new byte[81920];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    totalRead += bytesRead;
                    string sizeInfo = totalBytes > 0 
                        ? $"{totalRead / (1024 * 1024)} MB / {totalBytes / (1024 * 1024)} MB"
                        : $"{totalRead / (1024 * 1024)} MB";
                    progress?.Report(new UpdateProgress(totalRead, totalBytes, $"Downloading update ({sizeInfo})..."));
                }
            }

            progress?.Report(new UpdateProgress(totalRead, totalBytes, "Download completed. Verifying package..."));
            return targetFile;
        }
    }

    public async Task<UpdateInfo> GetSimulatedUpdateInfoAsync()
    {
        await Task.Yield();
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string setupExe = Path.Combine(baseDir, "SecApperFolderLockerSetup_v1.1.0.exe");
        if (!File.Exists(setupExe))
        {
            setupExe = Path.Combine(baseDir, "..", "..", "..", "..", "..", "installer", "output", "SecApperFolderLockerSetup_v1.1.0.exe");
            if (File.Exists(setupExe)) setupExe = Path.GetFullPath(setupExe);
        }
        if (!File.Exists(setupExe))
        {
            string fallbackExe = Path.Combine(baseDir, "SecApperFolderLockerSetup.exe");
            if (File.Exists(fallbackExe)) setupExe = fallbackExe;
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
            Version = "1.2.0",
            ReleaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            DownloadUrl = File.Exists(setupExe) ? new Uri(setupExe).AbsoluteUri : "https://github.com/adilmahboobalam/Secappers/raw/main/installer/output/SecApperFolderLockerSetup_v1.1.0.exe",
            Sha256 = sha256,
            Mandatory = false,
            MinSupportedVersion = "1.0.0",
            ReleaseNotes = "SecApper v1.2.0 Live Update:\n• Fixed Master Security PIN configured on installation for universal folder unlock\n• Active double-click Windows Explorer unlock popup\n• Native NTFS Discretionary Access Control enforcement\n• Operation journal crash recovery\n• Background ransomware threat protection\n• Standalone zero-downtime auto-updater"
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
                    Arguments = $"/VERYSILENT /SUPPRESSMSGBOXES /SP- /NORESTART /FORCECLOSEAPPLICATIONS /CURRENTUSER /DIR=\"{baseDir}\"",
                    UseShellExecute = true
                };

                Process.Start(psi);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Normalizes user-entered or legacy manifest URLs.
    /// Converts GitHub repo/blob URLs (e.g. github.com/user/repo) into raw JSON endpoints (raw.githubusercontent.com/user/repo/main/latest.json).
    /// </summary>
    public static string NormalizeManifestUrl(string? url)
    {
        const string DefaultRawUrl = "https://raw.githubusercontent.com/adilmahboobalam/Secappers/main/latest.json";

        if (string.IsNullOrWhiteSpace(url))
        {
            return DefaultRawUrl;
        }

        url = url.Trim();

        if (url.Contains("updates.secapper.com", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultRawUrl;
        }

        // Keep local file references intact
        if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) || File.Exists(url))
        {
            return url;
        }

        // Normalize GitHub repository web links into raw GitHub user content links
        if (url.Contains("github.com", StringComparison.OrdinalIgnoreCase) && 
            !url.Contains("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(
                url, 
                @"github\.com/(?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?(?:/(?:blob|raw|tree)/(?<branch>[^/]+)(?:/(?<path>.+))?)?/?$", 
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                string owner = match.Groups["owner"].Value;
                string repo = match.Groups["repo"].Value;
                string branch = match.Groups["branch"].Success && !string.IsNullOrWhiteSpace(match.Groups["branch"].Value)
                    ? match.Groups["branch"].Value
                    : "main";
                string path = match.Groups["path"].Success && !string.IsNullOrWhiteSpace(match.Groups["path"].Value)
                    ? match.Groups["path"].Value
                    : "latest.json";

                if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    path = path.TrimEnd('/') + "/latest.json";
                }

                return $"https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{path}";
            }
        }

        return url;
    }
}
