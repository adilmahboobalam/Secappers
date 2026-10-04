using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
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

            // Write desktop.ini referencing our locked.ico
            string iniContent = $"[.ShellClassInfo]\r\nIconResource={_iconPath},0\r\n[ViewState]\r\nMode=\r\nVid=\r\nFolderType=Generic\r\n";
            File.WriteAllText(desktopIni, iniContent, Encoding.Unicode);

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
            if (File.Exists(iconPath)) return;

            string? dir = Path.GetDirectoryName(iconPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Generate crisp padlock icon in memory and save as valid .ICO
            byte[] icoBytes = GeneratePadlockIconBytes();
            File.WriteAllBytes(iconPath, icoBytes);
        }
        catch
        {
            // Ignore if creation fails due to non-critical restriction
        }
    }

    private static byte[] GeneratePadlockIconBytes()
    {
        int size = 48;
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Outer dark security shield/badge background
            using var bgBrush = new SolidBrush(Color.FromArgb(240, 20, 25, 35));
            using var borderPen = new Pen(Color.FromArgb(255, 235, 87, 87), 2.5f);
            
            // Draw rounded badge
            using var path = new GraphicsPath();
            float r = 8f;
            RectangleF rect = new(3, 3, size - 6, size - 6);
            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();

            g.FillPath(bgBrush, path);
            g.DrawPath(borderPen, path);

            // Shackle (padlock top loop)
            using var shacklePen = new Pen(Color.FromArgb(255, 240, 195, 48), 3.5f);
            g.DrawArc(shacklePen, 17, 12, 14, 14, 180, 180);
            g.DrawLine(shacklePen, 17, 19, 17, 24);
            g.DrawLine(shacklePen, 31, 19, 31, 24);

            // Lock body
            using var bodyBrush = new LinearGradientBrush(
                new PointF(13, 23),
                new PointF(35, 39),
                Color.FromArgb(255, 243, 156, 18),
                Color.FromArgb(255, 211, 84, 0));
            
            Rectangle lockBody = new(13, 22, 22, 16);
            g.FillRectangle(bodyBrush, lockBody);

            // Keyhole
            using var keyHoleBrush = new SolidBrush(Color.FromArgb(255, 20, 20, 20));
            g.FillEllipse(keyHoleBrush, 22, 26, 4, 4);
            g.FillRectangle(keyHoleBrush, 23, 29, 2, 5);
        }

        // Encode as PNG into memory
        using var pngStream = new MemoryStream();
        bitmap.Save(pngStream, ImageFormat.Png);
        byte[] pngData = pngStream.ToArray();

        // Wrap PNG inside ICO container
        using var icoStream = new MemoryStream();
        using var writer = new BinaryWriter(icoStream);

        // ICONDIR
        writer.Write((ushort)0); // reserved
        writer.Write((ushort)1); // type 1 = icon
        writer.Write((ushort)1); // 1 image

        // ICONDIRENTRY
        writer.Write((byte)size); // width
        writer.Write((byte)size); // height
        writer.Write((byte)0);    // color count
        writer.Write((byte)0);    // reserved
        writer.Write((ushort)1);  // color planes
        writer.Write((ushort)32); // bpp
        writer.Write((uint)pngData.Length); // size of image data
        writer.Write((uint)22);   // offset (6 + 16 = 22)

        // Image data (PNG bytes)
        writer.Write(pngData);

        return icoStream.ToArray();
    }
}
