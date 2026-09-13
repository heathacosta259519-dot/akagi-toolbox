using System.Runtime.InteropServices;
using ContextMenuEditor.Models;

namespace ContextMenuEditor.Services;

public sealed class IconService : IDisposable
{
    private const uint SHGFI_ICON = 0x00000100;
    private const uint SHGFI_SMALLICON = 0x00000001;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint fileAttributes, ref ShFileInfo fileInfo, uint fileInfoSize, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    private readonly Dictionary<string, Image?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public Image? ForEntry(MenuEntry entry) => ForPath(entry.IconSource);

    public Image? ForPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (_cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var image = Load(path);
        _cache[path] = image;
        return image;
    }

    private static Image? Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var info = new ShFileInfo();
            var result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), SHGFI_ICON | SHGFI_SMALLICON);
            if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                using var icon = Icon.FromHandle(info.hIcon);
                return icon.ToBitmap();
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var image in _cache.Values)
        {
            image?.Dispose();
        }

        _cache.Clear();
    }
}
