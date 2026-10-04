using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;

namespace SecApper.Security.Security;

public class AclService : IAclService
{
    // The exact permissions denied during lock: blocks all content reading, directory enumeration, writing, execution, and deletion,
    // while deliberately preserving ChangePermissions & ReadPermissions so the authorized owner/service can unlock the folder.
    public const FileSystemRights LockedDeniedRights = 
        FileSystemRights.ReadAndExecute | 
        FileSystemRights.Modify | 
        FileSystemRights.ListDirectory | 
        FileSystemRights.CreateFiles | 
        FileSystemRights.CreateDirectories | 
        FileSystemRights.Delete | 
        FileSystemRights.DeleteSubdirectoriesAndFiles;

    public string GetCurrentSddl(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Directory not found: {folderPath}");

        DirectoryInfo dInfo = new(folderPath);
        AccessControlSections sections = AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group;
        DirectorySecurity security = dInfo.GetAccessControl(sections);
        return security.GetSecurityDescriptorSddlForm(sections);
    }

    public AclOperationResult ApplyLockAcl(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath))
                return new AclOperationResult(false, $"Folder does not exist: {folderPath}");

            DirectoryInfo dInfo = new(folderPath);
            DirectorySecurity security = dInfo.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);

            SecurityIdentifier? currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser != null)
            {
                // Add explicit Deny rule for current user
                var userDenyRule = new FileSystemAccessRule(
                    currentUser,
                    LockedDeniedRights,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Deny);

                security.AddAccessRule(userDenyRule);
            }

            // Also deny Authenticated Users
            SecurityIdentifier authenticatedUsers = new(WellKnownSidType.AuthenticatedUserSid, null);
            var authUsersDenyRule = new FileSystemAccessRule(
                authenticatedUsers,
                LockedDeniedRights,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Deny);

            security.AddAccessRule(authUsersDenyRule);

            dInfo.SetAccessControl(security);
            return new AclOperationResult(true, null);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new AclOperationResult(false, $"Access denied while applying permissions. Administrator privileges may be required: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new AclOperationResult(false, $"Failed to apply ACL lock: {ex.Message}");
        }
    }

    public AclOperationResult RestoreAcl(string folderPath, string sddl)
    {
        try
        {
            if (!Directory.Exists(folderPath))
                return new AclOperationResult(false, $"Folder does not exist: {folderPath}");

            if (string.IsNullOrWhiteSpace(sddl))
                return new AclOperationResult(false, "Restoration SDDL string is empty.");

            // 1. Primary restoration attempt: Full SDDL (Access, Owner, Group)
            try
            {
                DirectorySecurity restoredSecurity = new();
                restoredSecurity.SetSecurityDescriptorSddlForm(sddl, AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group);

                DirectoryInfo dInfo = new(folderPath);
                dInfo.SetAccessControl(restoredSecurity);

                return new AclOperationResult(true, null);
            }
            catch (Exception ex)
            {
                // 2. Secondary fallback: DACL only (if Owner/Group modification is restricted)
                try
                {
                    DirectorySecurity daclSecurity = new();
                    daclSecurity.SetSecurityDescriptorSddlForm(sddl, AccessControlSections.Access);
                    DirectoryInfo dInfo = new(folderPath);
                    dInfo.SetAccessControl(daclSecurity);
                    return new AclOperationResult(true, null);
                }
                catch (Exception daclEx)
                {
                    // 3. Guaranteed safety fallback: Rule-based removal of explicit Deny rules
                    try
                    {
                        DirectoryInfo dInfo = new(folderPath);
                        DirectorySecurity sec = dInfo.GetAccessControl(AccessControlSections.Access);
                        var rules = sec.GetAccessRules(true, false, typeof(SecurityIdentifier));
                        bool removedAny = false;

                        foreach (FileSystemAccessRule rule in rules)
                        {
                            if (rule.AccessControlType == AccessControlType.Deny)
                            {
                                sec.RemoveAccessRule(rule);
                                removedAny = true;
                            }
                        }

                        if (removedAny)
                        {
                            dInfo.SetAccessControl(sec);
                            return new AclOperationResult(true, null);
                        }

                        return new AclOperationResult(false, $"Failed to restore original permissions: {daclEx.Message} (Primary: {ex.Message})");
                    }
                    catch (Exception ruleEx)
                    {
                        return new AclOperationResult(false, $"Failed to restore permissions through all fallbacks: {ruleEx.Message} (DACL error: {daclEx.Message}, Primary: {ex.Message})");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return new AclOperationResult(false, $"Unexpected error restoring ACL: {ex.Message}");
        }
    }

    public bool VerifyIsLocked(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath)) return false;

            // Attempt to enumerate entries inside
            _ = Directory.EnumerateFileSystemEntries(folderPath).FirstOrDefault();

            // Attempt to get files via DirectoryInfo
            DirectoryInfo dInfo = new(folderPath);
            _ = dInfo.GetFiles();

            // If we could access the directory contents, it is NOT locked
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // Access denied confirms the folder is locked!
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch
        {
            // Any other security/access failure indicates restricted access
            return true;
        }
    }

    public bool VerifyIsAccessible(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath)) return false;

            _ = Directory.EnumerateFileSystemEntries(folderPath).FirstOrDefault();
            DirectoryInfo dInfo = new(folderPath);
            _ = dInfo.GetFiles();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
