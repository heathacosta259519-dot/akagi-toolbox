using ContextMenuEditor.Models;
using ContextMenuEditor.Services;
using Xunit;

namespace ContextMenuEditor.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        "context-menu-editor-settings-" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void Defaults_to_simple_mode_with_files_scene()
    {
        var settings = new SettingsService(_path);

        Assert.True(settings.SimpleMode);
        Assert.Equal(MenuScene.Files, settings.Scene);
    }

    [Fact]
    public void Update_then_reload_round_trips()
    {
        var settings = new SettingsService(_path);
        settings.Update(false, MenuScene.Drive);

        var reloaded = new SettingsService(_path);

        Assert.False(reloaded.SimpleMode);
        Assert.Equal(MenuScene.Drive, reloaded.Scene);
    }

    [Fact]
    public void Corrupted_file_falls_back_to_defaults()
    {
        File.WriteAllText(_path, "{ this is not json");

        var settings = new SettingsService(_path);

        Assert.True(settings.SimpleMode);
        Assert.Equal(MenuScene.Files, settings.Scene);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
