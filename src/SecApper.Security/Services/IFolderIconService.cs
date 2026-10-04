namespace SecApper.Security.Services;

public interface IFolderIconService
{
    string GetLockedIconPath();
    bool SetLockedIcon(string folderPath);
    bool RestoreDefaultIcon(string folderPath);
}
