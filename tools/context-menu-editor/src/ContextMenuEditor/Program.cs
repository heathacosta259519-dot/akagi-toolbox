using System.Text;
using System.Text.Json;
using ContextMenuEditor.Forms;
using ContextMenuEditor.Models;
using ContextMenuEditor.Probe;

namespace ContextMenuEditor;

internal static class Program
{
    private const string MutexName = @"Local\ContextMenuEditor.SingleInstance";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 4 && args[0] == "--probe")
        {
            return RunProbe(args[1], args[2], args[3]);
        }

        if (args.Length >= 6 && args[0] == "--probe-handlers")
        {
            return RunHandlerProbe(args[1], args[2], args[3], args[4], args[5]);
        }

        using var mutex = new Mutex(true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("右键菜单编辑器已在运行。", "右键菜单编辑器", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(new Font("Microsoft YaHei UI", 9F));

        Application.Run(new MainForm());
        return 0;
    }

    private static int RunProbe(string scene, string target, string outputPath)
    {
        MenuProbeResult result;
        try
        {
            result = MenuProbe.Run(scene, target);
        }
        catch (Exception exception)
        {
            result = new MenuProbeResult { Scene = scene, Target = target, Error = exception.ToString() };
        }

        WriteJson(outputPath, result);
        return 0;
    }

    private static int RunHandlerProbe(string scene, string target, string clsidList, string commandClsidList, string outputPath)
    {
        HandlerProbeResult result;
        try
        {
            result = HandlerProbe.Run(
                scene,
                target,
                Split(clsidList),
                Split(commandClsidList));
        }
        catch (Exception exception)
        {
            result = new HandlerProbeResult
            {
                Handlers = [new HandlerProbeEntry { Clsid = string.Empty, Error = exception.ToString() }],
            };
        }

        WriteJson(outputPath, result);
        return 0;
    }

    private static IEnumerable<string> Split(string list) =>
        list.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct();

    private static void WriteJson<T>(string outputPath, T payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });

        try
        {
            File.WriteAllText(outputPath, json, new UTF8Encoding(false));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
