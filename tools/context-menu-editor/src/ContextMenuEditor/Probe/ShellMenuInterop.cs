using System.Runtime.InteropServices;
using System.Text;

namespace ContextMenuEditor.Probe;

internal static class ShellMenuInterop
{
    internal const uint CmfNormal = 0x00000000;
    internal const uint GcsVerbW = 0x00000004;

    internal const uint MiimState = 0x00000001;
    internal const uint MiimId = 0x00000002;
    internal const uint MiimSubmenu = 0x00000004;
    internal const uint MiimData = 0x00000020;
    internal const uint MiimString = 0x00000040;
    internal const uint MiimFtype = 0x00000100;

    internal const uint MftSeparator = 0x00000800;
    internal const uint MftOwnerDraw = 0x00000100;
    internal const uint MfsGrayed = 0x00000003;
    internal const uint MfsDisabled = 0x00000003;
    internal const uint MfsChecked = 0x00000008;

    internal const uint WmInitMenuPopup = 0x0117;

    internal static readonly Guid IidIShellFolder = new("000214E6-0000-0000-C000-000000000046");
    internal static readonly Guid IidIContextMenu = new("000214E4-0000-0000-C000-000000000046");
    internal static readonly Guid IidIContextMenu2 = new("000214F4-0000-0000-C000-000000000046");
    internal static readonly Guid IidIContextMenu3 = new("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719");
    internal static readonly Guid IidIUnknown = new("00000000-0000-0000-C000-000000000046");
    internal static readonly Guid IidIDataObject = new("0000010E-0000-0000-C000-000000000046");

    [DllImport("ole32.dll")]
    internal static extern int CoCreateInstance(ref Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, ref Guid riid, out IntPtr ppv);

    [DllImport("shell32.dll")]
    internal static extern IntPtr ILClone(IntPtr pidl);

    [DllImport("shell32.dll")]
    internal static extern bool ILRemoveLastID(IntPtr pidl);

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E8-0000-0000-C000-000000000046")]
    internal interface IShellExtInit
    {
        void Initialize(IntPtr pidlFolder, IntPtr pdtobj, IntPtr hkeyProgID);
    }

    [DllImport("shell32.dll")]
    internal static extern int SHGetDesktopFolder(out IShellFolder ppshf);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHParseDisplayName(
        string pszName,
        IntPtr pbc,
        out IntPtr ppidl,
        uint sfgaoIn,
        out uint psfgaoOut);

    [DllImport("shell32.dll")]
    internal static extern int SHBindToParent(IntPtr pidl, ref Guid riid, out IntPtr ppv, out IntPtr ppidlLast);

    [DllImport("user32.dll")]
    internal static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    internal static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    internal static extern int GetMenuItemCount(IntPtr hMenu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetMenuString(IntPtr hMenu, uint uIDItem, StringBuilder lpString, int cchMax, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool GetMenuItemInfo(IntPtr hMenu, uint uItem, bool fByPosition, ref MenuItemInfo lpmii);

    [DllImport("ole32.dll")]
    internal static extern void CoTaskMemFree(IntPtr ptr);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MenuItemInfo
    {
        public uint cbSize;
        public uint fMask;
        public uint fType;
        public uint fState;
        public uint wID;
        public IntPtr hSubMenu;
        public IntPtr hbmpChecked;
        public IntPtr hbmpUnchecked;
        public IntPtr dwItemData;
        public IntPtr dwTypeData;
        public uint cch;
        public IntPtr hbmpItem;
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    internal interface IShellFolder
    {
        void ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, out uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);

        void EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);

        void BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        void BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        void CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);

        void CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr ppv);

        void GetAttributesOf(uint cidl, IntPtr apidl, ref uint rgfInOut);

        void GetUIObjectOf(IntPtr hwndOwner, uint cidl, IntPtr apidl, ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);

        void GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);

        void SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E4-0000-0000-C000-000000000046")]
    internal interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

        void InvokeCommand(IntPtr pici);

        void GetCommandString(uint idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F4-0000-0000-C000-000000000046")]
    internal interface IContextMenu2
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

        void InvokeCommand(IntPtr pici);

        void GetCommandString(uint idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);

        [PreserveSig]
        int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719")]
    internal interface IContextMenu3
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

        void InvokeCommand(IntPtr pici);

        void GetCommandString(uint idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);

        [PreserveSig]
        int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);

        [PreserveSig]
        int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
    }
}
