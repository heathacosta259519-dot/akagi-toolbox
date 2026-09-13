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

    public string FileTarget { get; private set; } = string.Empty;

    public string FolderTarget { get; private set; } = string.Empty;

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
            FileTarget = dto.FileTarget ?? string.Empty;
            FolderTarget = dto.FolderTarget ?? string.Empty;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
        }
    }

    public void Update(bool simpleMode, MenuScene scene, string? fileTarget = null, string? folderTarget = null)
    {
        SimpleMode = simpleMode;
        Scene = scene;
        FileTarget = fileTarget ?? FileTarget;
        FolderTarget = folderTarget ?? FolderTarget;

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
                FileTarget = FileTarget,
                FolderTarget = FolderTarget,
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

        public string? FileTarget { get; set; }

        public string? FolderTarget { get; set; }
    }
}
