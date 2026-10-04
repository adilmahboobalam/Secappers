using System;
using System.Threading;
using System.Threading.Tasks;

namespace SecApper.Security.Updates;

public interface IUpdateService
{
    string CurrentVersion { get; }
    Task<UpdateCheckResult> CheckForUpdatesAsync(bool isManualCheck = false, CancellationToken ct = default);
    Task<string> DownloadUpdateAsync(UpdateInfo update, IProgress<UpdateProgress>? progress = null, CancellationToken ct = default);
    bool VerifyUpdatePackage(string filePath, UpdateInfo expectedUpdate, out string? errorMessage);
    Task<bool> LaunchUpdaterAndExitAsync(string packagePath, UpdateInfo update);
    Task<UpdateInfo> GetSimulatedUpdateInfoAsync();
}
