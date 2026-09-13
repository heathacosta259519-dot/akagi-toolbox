using ContextMenuEditor.Forms;

namespace ContextMenuEditor;

internal static class Program
{
    private const string MutexName = @"Local\ContextMenuEditor.SingleInstance";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("右键菜单编辑器已在运行。", "右键菜单编辑器", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(new Font("Microsoft YaHei UI", 9F));

        Application.Run(new MainForm());
    }
}
