using System.Diagnostics;
using Microsoft.Win32;

namespace ContextMenuEditor.Services;

public static class RegistryNavigator
{
    public static void Open(string registryPath)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Applets\Regedit"))
        {
            key.SetValue("LastKey", registryPath);
        }

        Process.Start(new ProcessStartInfo("regedit.exe") { UseShellExecute = true });
    }
}
