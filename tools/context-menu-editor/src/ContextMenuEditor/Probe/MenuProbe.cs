using System.Runtime.InteropServices;
using System.Text;
using ContextMenuEditor.Models;

namespace ContextMenuEditor.Probe;

public static class MenuProbe
{
    private const int MaxDepth = 4;
    private const uint IdCommandFirst = 1;
    private const uint IdCommandLast = 0x7FFF;

    public static MenuProbeResult Run(string scene, string target)
    {
        var result = new MenuProbeResult { Scene = scene, Target = target };
        IntPtr menu = IntPtr.Zero;
        ProbeHostWindow? host = null;

        try
        {
            host = new ProbeHostWindow();
            var contextMenu = ContextMenuFactory.Create(scene, target, host.Handle);
            menu = ShellMenuInterop.CreatePopupMenu();
            if (menu == IntPtr.Zero)
            {
                throw new InvalidOperationException("无法创建菜单句柄。");
            }

            contextMenu.Instance.QueryContextMenu(menu, 0, IdCommandFirst, IdCommandLast, ShellMenuInterop.CmfNormal);
            result.Items = Walk(menu, contextMenu, 0);
        }
        catch (Exception exception)
        {
            result.Error = exception.ToString();
        }
        finally
        {
            if (menu != IntPtr.Zero)
            {
                ShellMenuInterop.DestroyMenu(menu);
            }

            host?.Dispose();
        }

        return result;
    }

    private static List<MenuProbeItem> Walk(IntPtr menu, ContextMenuHandle contextMenu, int depth)
    {
        var items = new List<MenuProbeItem>();
        if (depth > MaxDepth)
        {
            return items;
        }

        var count = ShellMenuInterop.GetMenuItemCount(menu);
        for (uint index = 0; index < count; index++)
        {
            var info = new ShellMenuInterop.MenuItemInfo
            {
                cbSize = (uint)Marshal.SizeOf<ShellMenuInterop.MenuItemInfo>(),
                fMask = ShellMenuInterop.MiimId
                    | ShellMenuInterop.MiimSubmenu
                    | ShellMenuInterop.MiimFtype
                    | ShellMenuInterop.MiimState,
            };

            if (!ShellMenuInterop.GetMenuItemInfo(menu, index, true, ref info))
            {
                continue;
            }

            var item = new MenuProbeItem
            {
                Depth = depth,
                CommandId = info.wID,
                IsSeparator = (info.fType & ShellMenuInterop.MftSeparator) != 0,
                IsOwnerDraw = (info.fType & ShellMenuInterop.MftOwnerDraw) != 0,
                IsEnabled = (info.fState & ShellMenuInterop.MfsDisabled) == 0,
            };

            if (item.IsSeparator)
            {
                items.Add(item);
                continue;
            }

            item.Text = ReadText(menu, index);

            if (info.hSubMenu != IntPtr.Zero)
            {
                item.HasSubmenu = true;
                InitSubmenu(contextMenu.Instance, info.hSubMenu);
                item.Children = Walk(info.hSubMenu, contextMenu, depth + 1);
            }

            items.Add(item);
        }

        return items;
    }

    private static string ReadText(IntPtr menu, uint index)
    {
        var buffer = Marshal.AllocHGlobal(2048);
        try
        {
            var info = new ShellMenuInterop.MenuItemInfo
            {
                cbSize = (uint)Marshal.SizeOf<ShellMenuInterop.MenuItemInfo>(),
                fMask = ShellMenuInterop.MiimString,
                dwTypeData = buffer,
                cch = 1024,
            };

            if (!ShellMenuInterop.GetMenuItemInfo(menu, index, true, ref info))
            {
                return string.Empty;
            }

            var text = Marshal.PtrToStringUni(buffer) ?? string.Empty;
            return Clean(text);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal static string ReadTextAt(IntPtr menu, uint index) => ReadText(menu, index);

    internal static string CleanText(string raw) => Clean(raw);

    private sealed record ContextMenuHandle(IntPtr Pointer, ShellMenuInterop.IContextMenu Instance);

    private static void InitSubmenu(ShellMenuInterop.IContextMenu contextMenu, IntPtr submenu)
    {
        try
        {
            if (contextMenu is ShellMenuInterop.IContextMenu3 menu3)
            {
                menu3.HandleMenuMsg2(ShellMenuInterop.WmInitMenuPopup, submenu, IntPtr.Zero, out _);
                return;
            }

            if (contextMenu is ShellMenuInterop.IContextMenu2 menu2)
            {
                menu2.HandleMenuMsg(ShellMenuInterop.WmInitMenuPopup, submenu, IntPtr.Zero);
            }
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or NotSupportedException)
        {
        }
    }

    private static string Clean(string raw)
    {
        const string marker = "\u0001";
        var value = raw.Replace("&&", marker, StringComparison.Ordinal)
            .Replace("&", string.Empty, StringComparison.Ordinal)
            .Replace(marker, "&", StringComparison.Ordinal);
        return value.Replace("\t", " ").Trim();
    }

    private static class ContextMenuFactory
    {
        public static ContextMenuHandle Create(string scene, string target, IntPtr owner)
        {
            var isBackground = scene is "FolderBackground" or "DesktopBackground";

            var hr = ShellMenuInterop.SHParseDisplayName(target, IntPtr.Zero, out var pidl, 0, out _);
            if (hr != 0 || pidl == IntPtr.Zero)
            {
                throw new InvalidOperationException($"无法解析路径（0x{hr:X8}）：{target}");
            }

            try
            {
                var folderIid = ShellMenuInterop.IidIShellFolder;
                hr = ShellMenuInterop.SHBindToParent(pidl, ref folderIid, out var parentPointer, out var childPidl);
                if (hr != 0 || parentPointer == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"无法打开所在文件夹（0x{hr:X8}）：{target}");
                }

                var parent = (ShellMenuInterop.IShellFolder)Marshal.GetObjectForIUnknown(parentPointer);
                var menuIid = ShellMenuInterop.IidIContextMenu;

                if (isBackground)
                {
                    var folderIid2 = ShellMenuInterop.IidIShellFolder;
                    parent.BindToObject(childPidl, IntPtr.Zero, ref folderIid2, out var folderPointer);
                    if (folderPointer == IntPtr.Zero)
                    {
                        throw new InvalidOperationException($"无法打开文件夹：{target}");
                    }

                    var folder = (ShellMenuInterop.IShellFolder)Marshal.GetObjectForIUnknown(folderPointer);
                    folder.CreateViewObject(owner, ref menuIid, out var backgroundPointer);
                    if (backgroundPointer == IntPtr.Zero)
                    {
                        throw new InvalidOperationException($"系统没有为该位置提供右键菜单：{target}");
                    }

                    return Wrap(backgroundPointer);
                }

                var array = Marshal.AllocCoTaskMem(IntPtr.Size);
                try
                {
                    Marshal.WriteIntPtr(array, childPidl);
                    parent.GetUIObjectOf(owner, 1, array, ref menuIid, IntPtr.Zero, out var menuPointer);
                    if (menuPointer == IntPtr.Zero)
                    {
                        throw new InvalidOperationException($"系统没有为该目标提供右键菜单：{target}");
                    }

                    return Wrap(menuPointer);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(array);
                }
            }
            finally
            {
                ShellMenuInterop.CoTaskMemFree(pidl);
            }
        }

        private static ContextMenuHandle Wrap(IntPtr pointer) =>
            new(pointer, (ShellMenuInterop.IContextMenu)Marshal.GetObjectForIUnknown(pointer));
    }
}
