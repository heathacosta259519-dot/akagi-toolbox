using System.Runtime.InteropServices;
using System.Text;

namespace ContextMenuEditor.Services;

internal static class NativeMethods
{
    private const int EM_SETCUEBANNER = 0x1501;

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, int cchOutBuf, IntPtr ppvReserved);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

    public static void SetCueBanner(Control control, string text)
    {
        if (control.IsHandleCreated)
        {
            SendMessage(control.Handle, EM_SETCUEBANNER, 1, text);
        }
    }

    public static string? TryResolveIndirectString(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith('@'))
        {
            return null;
        }

        var buffer = new StringBuilder(1024);
        try
        {
            return SHLoadIndirectString(value, buffer, buffer.Capacity, IntPtr.Zero) == 0
                ? buffer.ToString()
                : null;
        }
        catch
        {
            return null;
        }
    }
}
