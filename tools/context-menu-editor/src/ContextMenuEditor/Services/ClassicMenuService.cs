using Microsoft.Win32;

namespace ContextMenuEditor.Services;

public static class ClassicMenuService
{
    private const string ClsidKeyPath = @"SOFTWARE\Classes\CLSID\" + RegistryPaths.ClassicMenuClsid;

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPaths.ClassicMenuKey);
        return key != null;
    }

    public static void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPaths.ClassicMenuKey);
        key.SetValue(null, string.Empty, RegistryValueKind.String);
    }

    public static void Disable()
    {
        Registry.CurrentUser.DeleteSubKeyTree(ClsidKeyPath, false);
    }
}
