using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace SecApper.Security.Services;

public class FolderIconService : IFolderIconService
{
    private const uint SHCNE_UPDATEITEM = 0x00002000;
    private const uint SHCNF_PATHW = 0x0005;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, string? dwItem1, IntPtr dwItem2);

    private readonly string _iconPath;

    public FolderIconService(string? customIconPath = null)
    {
        if (!string.IsNullOrEmpty(customIconPath))
        {
            _iconPath = customIconPath;
        }
        else
        {
            string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SecApper", "FolderLocker", "icons");
            try
            {
                Directory.CreateDirectory(baseDir);
            }
            catch
            {
                baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecApper", "FolderLocker", "icons");
                Directory.CreateDirectory(baseDir);
            }

            _iconPath = Path.Combine(baseDir, "locked.ico");
        }

        EnsureIconFileExists(_iconPath);
    }

    public string GetLockedIconPath() => _iconPath;

    public bool SetLockedIcon(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath)) return false;

            string desktopIni = Path.Combine(folderPath, "desktop.ini");

            // If desktop.ini already exists with hidden/system attributes, clear them first so we can overwrite
            if (File.Exists(desktopIni))
            {
                File.SetAttributes(desktopIni, FileAttributes.Normal);
            }

            // Write desktop.ini referencing our official SecApper icon
            string iniContent = $"[.ShellClassInfo]\r\nIconResource={_iconPath},0\r\n[ViewState]\r\nMode=\r\nVid=\r\nFolderType=Generic\r\n";
            File.WriteAllText(desktopIni, iniContent, Encoding.Unicode);

            // Configure desktop.ini ACL to prevent Deny inheritance so Windows Explorer can render our icon
            ConfigureDesktopIniAcl(desktopIni);

            // Windows Explorer requires desktop.ini to be Hidden + System
            File.SetAttributes(desktopIni, FileAttributes.Hidden | FileAttributes.System);

            // And the folder itself MUST have ReadOnly or System attribute set for Windows to parse desktop.ini
            var folderAttrs = File.GetAttributes(folderPath);
            File.SetAttributes(folderPath, folderAttrs | FileAttributes.ReadOnly);

            // Notify Windows Shell to refresh folder icon in Explorer
            NotifyShell(folderPath);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool RestoreDefaultIcon(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath)) return false;

            string desktopIni = Path.Combine(folderPath, "desktop.ini");
            if (File.Exists(desktopIni))
            {
                File.SetAttributes(desktopIni, FileAttributes.Normal);
                File.Delete(desktopIni);
            }

            // Remove ReadOnly attribute if present
            var folderAttrs = File.GetAttributes(folderPath);
            if ((folderAttrs & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(folderPath, folderAttrs & ~FileAttributes.ReadOnly);
            }

            // Notify Windows Shell
            NotifyShell(folderPath);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void ConfigureDesktopIniAcl(string desktopIni)
    {
        try
        {
            if (!File.Exists(desktopIni)) return;

            FileInfo fInfo = new(desktopIni);
            var iniAcl = fInfo.GetAccessControl();
            // Protect from inheritance so the locked folder's Deny ACEs do not propagate to desktop.ini
            iniAcl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            var authUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
            iniAcl.AddAccessRule(new FileSystemAccessRule(authUsers, FileSystemRights.ReadAndExecute, AccessControlType.Allow));

            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser != null)
            {
                iniAcl.AddAccessRule(new FileSystemAccessRule(currentUser, FileSystemRights.FullControl, AccessControlType.Allow));
            }

            var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            iniAcl.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, AccessControlType.Allow));

            var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            iniAcl.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, AccessControlType.Allow));

            fInfo.SetAccessControl(iniAcl);
        }
        catch
        {
            // Non-fatal if setting ACL on desktop.ini fails (e.g. non-NTFS volumes or mock test paths)
        }
    }

    private static void NotifyShell(string folderPath)
    {
        try
        {
            SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, folderPath, IntPtr.Zero);
        }
        catch
        {
            // Shell notification is non-fatal
        }
    }

    private static void EnsureIconFileExists(string iconPath)
    {
        try
        {
            byte[] officialBytes = GetOfficialIconBytes();

            // If file already exists and matches official icon size, we're all set
            if (File.Exists(iconPath))
            {
                var fileInfo = new FileInfo(iconPath);
                if (fileInfo.Length == officialBytes.Length)
                {
                    return;
                }

                // If outdated or procedural padlock, reset attributes so we can overwrite
                File.SetAttributes(iconPath, FileAttributes.Normal);
            }

            string? dir = Path.GetDirectoryName(iconPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(iconPath, officialBytes);
        }
        catch
        {
            // Ignore if creation fails due to non-critical restriction
        }
    }

    private static byte[] GetOfficialIconBytes()
    {
        // 1. Try reading from assembly embedded resource
        try
        {
            var assembly = typeof(FolderIconService).Assembly;
            var resourceNames = assembly.GetManifestResourceNames();
            foreach (var name in resourceNames)
            {
                if (name.EndsWith("app.ico", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = assembly.GetManifestResourceStream(name);
                    if (stream != null)
                    {
                        using var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        byte[] data = ms.ToArray();
                        if (data.Length > 0) return data;
                    }
                }
            }
        }
        catch { }

        // 2. Try locating on filesystem in standard app and build directories
        string[] candidatePaths =
        [
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", "app.ico"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "assets", "app.ico"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "assets", "app.ico")
        ];

        foreach (var path in candidatePaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    byte[] data = File.ReadAllBytes(path);
                    if (data.Length > 0) return data;
                }
            }
            catch { }
        }

        // 3. Fallback: Generate crisp SecApper shield icon in memory
        return GenerateSecApperShieldIconBytes();
    }

    private static byte[] GenerateSecApperShieldIconBytes()
    {
        int size = 48;
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // SecApper deep navy shield background (#091D38)
            using var bgBrush = new SolidBrush(Color.FromArgb(255, 9, 29, 56));
            using var borderPen = new Pen(Color.FromArgb(255, 18, 45, 85), 2f);

            // Shield polygon points
            PointF[] shieldPoints =
            [
                new PointF(24, 4),
                new PointF(42, 10),
                new PointF(42, 28),
                new PointF(24, 44),
                new PointF(6, 28),
                new PointF(6, 10)
            ];

            using var shieldPath = new GraphicsPath();
            shieldPath.AddPolygon(shieldPoints);
            g.FillPath(bgBrush, shieldPath);
            g.DrawPath(borderPen, shieldPath);

            // White stylized security crest & red accent (#C5202B)
            using var whiteBrush = new SolidBrush(Color.White);
            using var redBrush = new SolidBrush(Color.FromArgb(255, 197, 32, 43));

            // Inner stylized S/crest shape
            PointF[] sPoints =
            [
                new PointF(14, 15),
                new PointF(34, 15),
                new PointF(34, 21),
                new PointF(21, 21),
                new PointF(21, 25),
                new PointF(34, 25),
                new PointF(34, 33),
                new PointF(14, 33),
                new PointF(14, 27),
                new PointF(27, 27),
                new PointF(27, 23),
                new PointF(14, 23)
            ];
            g.FillPolygon(whiteBrush, sPoints);

            // Red accent dot/eye
            g.FillEllipse(redBrush, 28, 17, 4, 3);
        }

        // Encode as PNG into memory
        using var pngStream = new MemoryStream();
        bitmap.Save(pngStream, ImageFormat.Png);
        byte[] pngData = pngStream.ToArray();

        // Wrap PNG inside standard ICO container
        using var icoStream = new MemoryStream();
        using var writer = new BinaryWriter(icoStream);

        writer.Write((ushort)0); // reserved
        writer.Write((ushort)1); // type 1 = icon
        writer.Write((ushort)1); // 1 image

        writer.Write((byte)size);
        writer.Write((byte)size);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write((uint)pngData.Length);
        writer.Write((uint)22);

        writer.Write(pngData);

        return icoStream.ToArray();
    }
}
