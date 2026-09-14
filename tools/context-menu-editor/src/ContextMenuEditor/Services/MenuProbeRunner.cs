using System.Diagnostics;
using System.Text.Json;
using ContextMenuEditor.Models;

namespace ContextMenuEditor.Services;

public sealed class MenuReplica
{
    public MenuProbeResult? Menu { get; init; }

    public Dictionary<string, string> TextToClsid { get; init; } = new(StringComparer.CurrentCultureIgnoreCase);

    public string? Error { get; init; }

    public bool HasMenu => Menu is { Error: null, Items.Count: > 0 };
}

public sealed class MenuProbeRunner
{
    private readonly string _executable;
    private readonly string _workDirectory;
    private readonly object _gate = new();
    private readonly Dictionary<string, MenuProbeResult> _menus = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, List<string>>> _handlerMaps = new(StringComparer.OrdinalIgnoreCase);

    public MenuProbeRunner(string executable)
    {
        _executable = executable;
        _workDirectory = Path.Combine(Path.GetTempPath(), "ContextMenuEditor");
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _menus.Clear();
            _handlerMaps.Clear();
        }
    }

    public MenuProbeResult? TryGetMenu(MenuScene scene, string target)
    {
        lock (_gate)
        {
            return _menus.TryGetValue(MenuKey(scene, target), out var cached) ? cached : null;
        }
    }

    public Dictionary<string, List<string>>? TryGetHandlerMap(MenuScene scene, string target, IReadOnlyList<string> clsids, IReadOnlyList<string> commandClsids)
    {
        lock (_gate)
        {
            return _handlerMaps.TryGetValue(HandlerKey(scene, target, clsids, commandClsids), out var cached) ? cached : null;
        }
    }

    public MenuProbeResult GetMenu(MenuScene scene, string target)
    {
        var key = MenuKey(scene, target);
        lock (_gate)
        {
            if (_menus.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var result = RunMenuProbe(scene, target);
        lock (_gate)
        {
            _menus[key] = result;
        }

        return result;
    }

    public Dictionary<string, List<string>> GetHandlerMap(
        MenuScene scene,
        string target,
        IReadOnlyList<string> clsids,
        IReadOnlyList<string> commandClsids)
    {
        var key = HandlerKey(scene, target, clsids, commandClsids);
        lock (_gate)
        {
            if (_handlerMaps.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var map = RunHandlerProbe(scene, target, clsids, commandClsids);
        lock (_gate)
        {
            _handlerMaps[key] = map;
        }

        return map;
    }

    private static string MenuKey(MenuScene scene, string target) => $"{scene}|{target}";

    private static string HandlerKey(MenuScene scene, string target, IReadOnlyList<string> clsids, IReadOnlyList<string> commandClsids) =>
        $"{scene}|{target}|{string.Join(";", clsids)}|{string.Join(";", commandClsids)}";

    private MenuProbeResult RunMenuProbe(MenuScene scene, string target)
    {
        var json = RunProbe(["--probe", scene.ToString(), target], TimeSpan.FromSeconds(25));
        if (json == null)
        {
            return new MenuProbeResult { Scene = scene.ToString(), Target = target, Error = "菜单探测超时或失败" };
        }

        try
        {
            return JsonSerializer.Deserialize<MenuProbeResult>(json)
                ?? new MenuProbeResult { Scene = scene.ToString(), Target = target, Error = "菜单探测结果为空" };
        }
        catch (JsonException exception)
        {
            return new MenuProbeResult { Scene = scene.ToString(), Target = target, Error = exception.Message };
        }
    }

    private Dictionary<string, List<string>> RunHandlerProbe(
        MenuScene scene,
        string target,
        IReadOnlyList<string> clsids,
        IReadOnlyList<string> commandClsids)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.CurrentCultureIgnoreCase);
        if (clsids.Count == 0 && commandClsids.Count == 0)
        {
            return map;
        }

        var json = RunProbe(
            ["--probe-handlers", scene.ToString(), target, string.Join(";", clsids), string.Join(";", commandClsids)],
            TimeSpan.FromSeconds(40));
        if (json == null)
        {
            return map;
        }

        try
        {
            var result = JsonSerializer.Deserialize<HandlerProbeResult>(json);
            foreach (var handler in result?.Handlers ?? [])
            {
                if (string.IsNullOrWhiteSpace(handler.Clsid))
                {
                    continue;
                }

                foreach (var text in handler.Texts)
                {
                    if (!map.TryGetValue(text, out var owners))
                    {
                        owners = [];
                        map[text] = owners;
                    }

                    if (!owners.Contains(handler.Clsid, StringComparer.OrdinalIgnoreCase))
                    {
                        owners.Add(handler.Clsid);
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return map;
    }

    private string? RunProbe(string[] arguments, TimeSpan timeout)
    {
        Directory.CreateDirectory(_workDirectory);
        var outputPath = Path.Combine(_workDirectory, $"probe-{Guid.NewGuid():N}.json");

        try
        {
            var startInfo = new ProcessStartInfo(_executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _workDirectory,
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            startInfo.ArgumentList.Add(outputPath);

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                return null;
            }

            return File.Exists(outputPath) ? File.ReadAllText(outputPath) : null;
        }
        catch (Exception exception) when (exception is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
