using System.Collections.Generic;
using System.Threading.Tasks;
using SecApper.Security.Models;

namespace SecApper.Security.Recovery;

public record RecoveryIssue(
    FolderRecord Folder,
    string Description,
    bool DiskIsLocked,
    bool FolderExists);

public interface IRecoveryService
{
    Task<List<RecoveryIssue>> ScanForInconsistenciesAsync();
    Task<bool> RepairProtectionAsync(string folderId);
    Task<bool> RestoreAccessAsync(string folderId);
}
