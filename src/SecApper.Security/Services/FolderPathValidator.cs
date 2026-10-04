using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SecApper.Security.Services;

public class FolderPathValidator : IFolderPathValidator
{
    private readonly HashSet<string> _prohibitedPaths = new(StringComparer.OrdinalIgnoreCase);

    public FolderPathValidator(string? customAppDir = null, string? customDataDir = null)
    {
        InitializeProhibitedPaths(customAppDir, customDataDir);
    }

    private void InitializeProhibitedPaths(string? customAppDir, string? customDataDir)
    {
        // System and sensitive directories
        AddProhibited(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        AddProhibited(Environment.GetFolderPath(Environment.SpecialFolder.System));
        AddProhibited(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddProhibited(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        AddProhibited(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)); // C:\ProgramData
        AddProhibited(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); // C:\Users\Username root

        // App directory and data directory
        string appDir = customAppDir ?? AppDomain.CurrentDomain.BaseDirectory;
        AddProhibited(appDir);

        if (!string.IsNullOrEmpty(customDataDir))
        {
            AddProhibited(customDataDir);
        }
        else
        {
            string programDataSecApper = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SecApper");
            AddProhibited(programDataSecApper);
            string localAppSecApper = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecApper");
            AddProhibited(localAppSecApper);
        }
    }

    private void AddProhibited(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                string normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                _prohibitedPaths.Add(normalized);
            }
            catch
            {
                // Ignore invalid paths in initialization
            }
        }
    }

    public ValidationResult ValidatePath(string path, IEnumerable<string> existingRegisteredPaths)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new ValidationResult(false, "Folder path cannot be empty.");

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Invalid folder path format: {ex.Message}");
        }

        // Must exist and be a directory
        if (!Directory.Exists(fullPath))
            return new ValidationResult(false, "The selected path does not exist or is not a directory.");

        // Must not be a root directory (e.g., C:\ or D:\)
        string? root = Path.GetPathRoot(fullPath);
        if (root != null && string.Equals(fullPath, root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            return new ValidationResult(false, "Cannot lock an entire drive root (e.g. C:\\ or D:\\). Please select a specific folder.");

        // Check if symbolic link or reparse point (warn/disallow to avoid security issues)
        try
        {
            DirectoryInfo dirInfo = new(fullPath);
            if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return new ValidationResult(false, "The selected folder is a junction or symbolic link. Symbolic links cannot be locked directly.");
            }
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Could not inspect folder attributes: {ex.Message}");
        }

        // Check if prohibited system/app folder
        foreach (string prohibited in _prohibitedPaths)
        {
            if (string.Equals(fullPath, prohibited, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult(false, "This is a critical Windows or SecApper system folder and cannot be locked.");
            }

            // Also prevent locking parent directories of prohibited paths (e.g. C:\Users)
            if (prohibited.StartsWith(fullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult(false, "The selected folder contains critical system components or user profiles and cannot be locked.");
            }
        }

        // Check against existing registered folders
        if (existingRegisteredPaths != null)
        {
            foreach (string existing in existingRegisteredPaths)
            {
                if (string.IsNullOrWhiteSpace(existing)) continue;

                string normalizedExisting = Path.GetFullPath(existing).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (string.Equals(fullPath, normalizedExisting, StringComparison.OrdinalIgnoreCase))
                {
                    return new ValidationResult(false, "This folder is already registered in SecApper Folder Locker.");
                }

                // Check nested child: cannot register D:\A\B if D:\A is registered
                if (fullPath.StartsWith(normalizedExisting + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return new ValidationResult(false, $"This folder is already inside protected folder: {normalizedExisting}");
                }

                // Check parent: cannot register D:\A if D:\A\B is registered
                if (normalizedExisting.StartsWith(fullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return new ValidationResult(false, $"The subfolder '{normalizedExisting}' is already protected. Remove it before locking the parent folder.");
                }
            }
        }

        // Check filesystem type
        if (!IsNtfsFileSystem(fullPath))
        {
            return new ValidationResult(false, "The drive hosting this folder does not use the NTFS file system. Windows Access Control Lists (ACL) require NTFS.");
        }

        return new ValidationResult(true, null);
    }

    public bool IsNtfsFileSystem(string path)
    {
        try
        {
            string? root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root)) return false;

            DriveInfo drive = new(root);
            return string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // If drive info cannot be queried, allow fallback if local drive
            return true;
        }
    }
}
