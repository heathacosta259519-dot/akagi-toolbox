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
    private MenuProbeResult? _cachedMenu;
    private Dictionary<string, string>? _cachedHandlers;
    private string? _cachedKey;
    private string? _cachedHandlerKey;

    public MenuProbeRunner(string executable)
    {
        _executable = executable;
        _workDirectory = Path.Combine(Path.GetTempPath(), "ContextMenuEditor");
    }

    public MenuReplica Load(MenuScene scene, string target, IReadOnlyList<string> clsids, IReadOnlyList<string> commandClsids)
    {
        var key = $"{scene}|{target}";
        if (!string.Equals(_cachedKey, key, StringComparison.OrdinalIgnoreCase))
        {
            _cachedMenu = RunMenuProbe(scene, target);
            _cachedKey = key;
            _cachedHandlers = null;
            _cachedHandlerKey = null;
        }

        if (_cachedMenu is { Error: null })
        {
            var handlerKey = key + "|" + string.Join(";", clsids) + "|" + string.Join(";", commandClsids);
            if (!string.Equals(_cachedHandlerKey, handlerKey, StringComparison.OrdinalIgnoreCase))
            {
                _cachedHandlers = RunHandlerProbe(scene, target, clsids, commandClsids);
                _cachedHandlerKey = handlerKey;
            }
        }

        return new MenuReplica
        {
            Menu = _cachedMenu,
            TextToClsid = _cachedHandlers ?? new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase),
            Error = _cachedMenu?.Error,
        };
    }

    public void InvalidateMenu() => _cachedKey = null;

    private MenuProbeResult? RunMenuProbe(MenuScene scene, string target)
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

    private Dictionary<string, string> RunHandlerProbe(MenuScene scene, string target, IReadOnlyList<string> clsids, IReadOnlyList<string> commandClsids)
    {
        var map = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
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
                    map.TryAdd(text, handler.Clsid);
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
