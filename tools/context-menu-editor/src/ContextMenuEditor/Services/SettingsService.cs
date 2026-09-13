using System.Text.Json;
using ContextMenuEditor.Models;

namespace ContextMenuEditor.Services;

public sealed class SettingsService
{
    private readonly string _path;

    public SettingsService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ContextMenuEditor",
            "settings.json"))
    {
    }

    public SettingsService(string path)
    {
        _path = path;
        Reload();
    }

    public bool SimpleMode { get; private set; } = true;

    public MenuScene Scene { get; private set; } = MenuScene.Files;

    public void Reload()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }

            var dto = JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(_path));
            if (dto == null)
            {
                return;
            }

            SimpleMode = dto.SimpleMode ?? true;
            Scene = Enum.TryParse<MenuScene>(dto.Scene, out var scene) ? scene : MenuScene.Files;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
        }
    }

    public void Update(bool simpleMode, MenuScene scene)
    {
        SimpleMode = simpleMode;
        Scene = scene;

        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(new SettingsDto
            {
                SimpleMode = simpleMode,
                Scene = scene.ToString(),
            }));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed class SettingsDto
    {
        public bool? SimpleMode { get; set; }

        public string? Scene { get; set; }
    }
}
