using ContextMenuEditor.Models;
using ContextMenuEditor.Services;
using Xunit;

namespace ContextMenuEditor.Tests;

public sealed class SimpleMenuBuilderTests
{
    [Fact]
    public void Com_handlers_with_same_clsid_merge_into_one_item()
    {
        var entries = new[]
        {
            Com("{11111111-1111-1111-1111-111111111111}", RegistryViewKind.X64, HiveScope.Machine, LocationKind.AllFiles, "Sharing"),
            Com("{11111111-1111-1111-1111-111111111111}", RegistryViewKind.X86, HiveScope.Machine, LocationKind.AllFiles, "Sharing"),
            Com("{11111111-1111-1111-1111-111111111111}", RegistryViewKind.X64, HiveScope.Machine, LocationKind.AllFilesystemObjects, "Sharing"),
        };

        var item = Assert.Single(SimpleMenuBuilder.Build(entries, MenuScene.Files));

        Assert.Equal(3, item.Sources.Count);
        Assert.Equal("Sharing", item.DisplayName);
        Assert.True(item.IsShown);
        Assert.Equal("显示中", item.StateText);
    }

    [Fact]
    public void Scene_filters_items_by_location()
    {
        var entries = new[]
        {
            Static("static|bg", "Git Bash Here", LocationKind.DirectoryBackground),
            Static("static|file", "edit with notepad++", LocationKind.AllFiles),
        };

        var files = SimpleMenuBuilder.Build(entries, MenuScene.Files);
        Assert.Single(files);
        Assert.Equal("edit with notepad++", files[0].DisplayName);

        var background = SimpleMenuBuilder.Build(entries, MenuScene.FolderBackground);
        Assert.Single(background);
        Assert.Equal("Git Bash Here", background[0].DisplayName);

        Assert.Empty(SimpleMenuBuilder.Build(entries, MenuScene.Drive));
    }

    [Fact]
    public void Hidden_when_all_sources_disabled()
    {
        var entries = new[]
        {
            Com("{22222222-2222-2222-2222-222222222222}", RegistryViewKind.X64, HiveScope.Machine, LocationKind.AllFiles, "7-Zip", EntryState.Disabled),
        };

        var item = SimpleMenuBuilder.Build(entries, MenuScene.Files).Single();

        Assert.False(item.IsShown);
        Assert.Equal("已隐藏", item.StateText);
    }

    [Fact]
    public void Partially_hidden_state_is_reported()
    {
        var entries = new[]
        {
            Com("{33333333-3333-3333-3333-333333333333}", RegistryViewKind.X64, HiveScope.Machine, LocationKind.AllFiles, "Test", EntryState.Disabled),
            Com("{33333333-3333-3333-3333-333333333333}", RegistryViewKind.X86, HiveScope.Machine, LocationKind.AllFiles, "Test"),
        };

        var item = SimpleMenuBuilder.Build(entries, MenuScene.Files).Single();

        Assert.True(item.IsShown);
        Assert.True(item.IsPartiallyHidden);
        Assert.Equal("部分隐藏", item.StateText);
    }

    [Fact]
    public void Toggle_representatives_dedupe_by_view_and_prefer_machine()
    {
        var entries = new[]
        {
            Com("{44444444-4444-4444-4444-444444444444}", RegistryViewKind.X64, HiveScope.CurrentUser, LocationKind.AllFiles, "Test"),
            Com("{44444444-4444-4444-4444-444444444444}", RegistryViewKind.X64, HiveScope.Machine, LocationKind.AllFiles, "Test"),
            Com("{44444444-4444-4444-4444-444444444444}", RegistryViewKind.X86, HiveScope.CurrentUser, LocationKind.AllFiles, "Test"),
        };

        var representatives = SimpleMenuBuilder.Build(entries, MenuScene.Files).Single().ToggleRepresentatives().ToArray();

        Assert.Equal(2, representatives.Length);
        Assert.Contains(representatives, entry => entry.View == RegistryViewKind.X64 && entry.Scope == HiveScope.Machine);
        Assert.Contains(representatives, entry => entry.View == RegistryViewKind.X86);
    }

    [Fact]
    public void Explorer_command_items_are_marked_unsupported()
    {
        var entries = new[] { ExplorerCommand("static|yun", "baidunetdisk", LocationKind.AllFiles) };

        var item = SimpleMenuBuilder.Build(entries, MenuScene.Files).Single();

        Assert.True(item.IsUnsupported);
        Assert.Equal("暂不支持", item.StateText);
        Assert.Empty(item.ToggleRepresentatives());
    }

    [Fact]
    public void Orphan_blocked_entries_are_excluded()
    {
        var entries = new[]
        {
            new MenuEntry
            {
                Id = "orphan|CurrentUser|X64|{55555555-5555-5555-5555-555555555555}",
                DisplayName = "{55555555-5555-5555-5555-555555555555}",
                Kind = EntryKind.ComHandler,
                Scope = HiveScope.CurrentUser,
                View = RegistryViewKind.X64,
                Location = LocationKind.BlockedOrphan,
                RegistryPath = "HKCU\\...\\Blocked",
                KeyPath = string.Empty,
                Clsid = "{55555555-5555-5555-5555-555555555555}",
                State = EntryState.Orphan,
            },
        };

        Assert.Empty(SimpleMenuBuilder.Build(entries, MenuScene.Files));
    }

    private static MenuEntry Static(string id, string name, LocationKind location) => new()
    {
        Id = id,
        DisplayName = name,
        Kind = EntryKind.StaticVerb,
        Scope = HiveScope.Machine,
        View = RegistryViewKind.X64,
        Location = location,
        RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\...",
        KeyPath = "x",
    };

    private static MenuEntry Com(
        string clsid,
        RegistryViewKind view,
        HiveScope scope,
        LocationKind location,
        string name,
        EntryState state = EntryState.Enabled) => new()
    {
        Id = $"handler|{scope}|{view}|{location}|{name}",
        DisplayName = name,
        Kind = EntryKind.ComHandler,
        Scope = scope,
        View = view,
        Location = location,
        RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\...",
        KeyPath = "y",
        Clsid = clsid,
        State = state,
    };

    private static MenuEntry ExplorerCommand(string id, string name, LocationKind location) => new()
    {
        Id = id,
        DisplayName = name,
        Kind = EntryKind.ExplorerCommand,
        Scope = HiveScope.Machine,
        View = RegistryViewKind.X64,
        Location = location,
        RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\...",
        KeyPath = "z",
    };
}
