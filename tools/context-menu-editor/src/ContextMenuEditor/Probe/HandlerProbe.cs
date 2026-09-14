using System.Runtime.InteropServices;
using System.Text;
using ContextMenuEditor.Models;

namespace ContextMenuEditor.Probe;

public static class HandlerProbe
{
    private const uint ClsctxInprocServer = 0x1;
    private const uint IdCommandFirst = 1;
    private const uint IdCommandLast = 0x7FFF;
    private const int MaxDepth = 3;

    public static HandlerProbeResult Run(string scene, string target, IEnumerable<string> clsids, IEnumerable<string> commandClsids)
    {
        var result = new HandlerProbeResult();
        var isBackground = scene is "FolderBackground" or "DesktopBackground";
        var itemArray = CreateItemArray(target);

        using var host = new ProbeHostWindow();
        var selection = Selection.Create(target, host.Handle, isBackground);
        if (selection.Error != null)
        {
            foreach (var clsid in clsids)
            {
                result.Handlers.Add(new HandlerProbeEntry { Clsid = clsid, Error = selection.Error });
            }
        }
        else
        {
            foreach (var clsid in clsids)
            {
                result.Handlers.Add(ProbeOne(clsid, selection, host.Handle));
            }
        }

        foreach (var clsid in commandClsids)
        {
            result.Handlers.Add(ProbeExplorerCommand(clsid, itemArray));
        }

        return result;
    }

    private static IntPtr CreateItemArray(string target)
    {
        var itemIid = ShellMenuInterop.IidIShellItem;
        var hr = ShellMenuInterop.SHCreateItemFromParsingName(target, IntPtr.Zero, ref itemIid, out var item);
        if (hr != 0 || item == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var arrayIid = ShellMenuInterop.IidIShellItemArray;
        hr = ShellMenuInterop.SHCreateShellItemArrayFromShellItem(item, ref arrayIid, out var array);
        return hr == 0 ? array : IntPtr.Zero;
    }

    private static HandlerProbeEntry ProbeExplorerCommand(string clsid, IntPtr itemArray)
    {
        var entry = new HandlerProbeEntry { Clsid = clsid };

        try
        {
            var clsidGuid = new Guid(clsid);
            var iid = ShellMenuInterop.IidIExplorerCommand;
            var hr = ShellMenuInterop.CoCreateInstance(ref clsidGuid, IntPtr.Zero, ClsctxInprocServer, ref iid, out var pointer);
            if (hr != 0 || pointer == IntPtr.Zero)
            {
                entry.Error = $"无法创建命令（0x{hr:X8}）";
                return entry;
            }

            var command = (ShellMenuInterop.IExplorerCommand)Marshal.GetObjectForIUnknown(pointer);
            CollectTitles(command, itemArray, entry.Texts, 0);
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or NotSupportedException or ArgumentException or FormatException)
        {
            entry.Error = exception.Message;
        }

        return entry;
    }

    private static void CollectTitles(ShellMenuInterop.IExplorerCommand command, IntPtr itemArray, List<string> texts, int depth)
    {
        var title = ReadTitle(command, itemArray);
        if (!string.IsNullOrWhiteSpace(title))
        {
            texts.Add(title);
        }
    }

    private static string? ReadTitle(ShellMenuInterop.IExplorerCommand command, IntPtr itemArray)
    {
        var hr = command.GetTitle(itemArray, out var pointer);
        if (hr != 0 || pointer == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var title = Marshal.PtrToStringUni(pointer);
            var cleaned = string.IsNullOrWhiteSpace(title) ? null : MenuProbe.CleanText(title);
            return string.IsNullOrEmpty(cleaned) ? null : cleaned;
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    private static HandlerProbeEntry ProbeOne(string clsid, Selection selection, IntPtr owner)
    {
        var entry = new HandlerProbeEntry { Clsid = clsid };

        try
        {
            var clsidGuid = new Guid(clsid);
            var unknownIid = ShellMenuInterop.IidIUnknown;
            var hr = ShellMenuInterop.CoCreateInstance(ref clsidGuid, IntPtr.Zero, ClsctxInprocServer, ref unknownIid, out var unknown);
            if (hr != 0 || unknown == IntPtr.Zero)
            {
                entry.Error = $"无法创建扩展（0x{hr:X8}）";
                return entry;
            }

            var extInit = (ShellMenuInterop.IShellExtInit)Marshal.GetObjectForIUnknown(unknown);
            extInit.Initialize(selection.FolderPidl, selection.DataObject, IntPtr.Zero);

            var contextMenu = (ShellMenuInterop.IContextMenu)Marshal.GetObjectForIUnknown(unknown);
            var menu = ShellMenuInterop.CreatePopupMenu();
            if (menu == IntPtr.Zero)
            {
                entry.Error = "无法创建菜单句柄";
                return entry;
            }

            try
            {
                contextMenu.QueryContextMenu(menu, 0, IdCommandFirst, IdCommandLast, ShellMenuInterop.CmfNormal);
                Collect(menu, 0, entry.Texts);
            }
            finally
            {
                ShellMenuInterop.DestroyMenu(menu);
            }
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or NotSupportedException or ArgumentException or FormatException)
        {
            entry.Error = exception.Message;
        }

        return entry;
    }

    private static void Collect(IntPtr menu, int depth, List<string> texts)
    {
        if (depth > MaxDepth)
        {
            return;
        }

        var count = ShellMenuInterop.GetMenuItemCount(menu);
        for (uint index = 0; index < count; index++)
        {
            var info = new ShellMenuInterop.MenuItemInfo
            {
                cbSize = (uint)Marshal.SizeOf<ShellMenuInterop.MenuItemInfo>(),
                fMask = ShellMenuInterop.MiimSubmenu | ShellMenuInterop.MiimFtype,
            };

            if (!ShellMenuInterop.GetMenuItemInfo(menu, index, true, ref info))
            {
                continue;
            }

            if ((info.fType & ShellMenuInterop.MftSeparator) != 0)
            {
                continue;
            }

            var text = MenuProbe.ReadTextAt(menu, index);
            if (text.Length > 0)
            {
                texts.Add(text);
            }

            if (info.hSubMenu != IntPtr.Zero)
            {
                Collect(info.hSubMenu, depth + 1, texts);
            }
        }
    }

    private sealed class Selection : IDisposable
    {
        private readonly IntPtr _pidl;
        private readonly IntPtr _folderPidl;
        private readonly IntPtr _parentPointer;

        private Selection(IntPtr pidl, IntPtr folderPidl, IntPtr parentPointer, IntPtr dataObject, bool isBackground)
        {
            _pidl = pidl;
            _folderPidl = folderPidl;
            _parentPointer = parentPointer;
            DataObject = dataObject;
            IsBackground = isBackground;
        }

        public IntPtr FolderPidl => IsBackground ? _pidl : _folderPidl;

        public IntPtr DataObject { get; }

        public bool IsBackground { get; }

        public string? Error { get; private set; }

        public static Selection Create(string target, IntPtr owner, bool isBackground)
        {
            var hr = ShellMenuInterop.SHParseDisplayName(target, IntPtr.Zero, out var pidl, 0, out _);
            if (hr != 0 || pidl == IntPtr.Zero)
            {
                return new Selection(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, isBackground)
                {
                    Error = $"无法解析路径（0x{hr:X8}）",
                };
            }

            var folderPidl = ShellMenuInterop.ILClone(pidl);
            ShellMenuInterop.ILRemoveLastID(folderPidl);

            var folderIid = ShellMenuInterop.IidIShellFolder;
            hr = ShellMenuInterop.SHBindToParent(pidl, ref folderIid, out var parentPointer, out var childPidl);
            if (hr != 0 || parentPointer == IntPtr.Zero)
            {
                return new Selection(pidl, folderPidl, IntPtr.Zero, IntPtr.Zero, isBackground)
                {
                    Error = $"无法打开所在文件夹（0x{hr:X8}）",
                };
            }

            var dataObject = IntPtr.Zero;
            if (!isBackground)
            {
                var parent = (ShellMenuInterop.IShellFolder)Marshal.GetObjectForIUnknown(parentPointer);
                var array = Marshal.AllocCoTaskMem(IntPtr.Size);
                try
                {
                    Marshal.WriteIntPtr(array, childPidl);
                    var dataObjectIid = ShellMenuInterop.IidIDataObject;
                    parent.GetUIObjectOf(owner, 1, array, ref dataObjectIid, IntPtr.Zero, out dataObject);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(array);
                }
            }

            return new Selection(pidl, folderPidl, parentPointer, dataObject, isBackground);
        }

        public void Dispose()
        {
            if (_pidl != IntPtr.Zero)
            {
                ShellMenuInterop.CoTaskMemFree(_pidl);
            }

            if (_folderPidl != IntPtr.Zero)
            {
                ShellMenuInterop.CoTaskMemFree(_folderPidl);
            }

            if (_parentPointer != IntPtr.Zero)
            {
                Marshal.Release(_parentPointer);
            }
        }
    }
}
