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
    public void Same_verb_in_both_scopes_merges_into_one_menu_item()
    {
        var entries = new[]
        {
            Static("static|Machine|*|bvshell", "bvshell", LocationKind.AllFiles, scope: HiveScope.Machine, keyPath: @"*\shell\bvshell"),
            Static("static|CurrentUser|*|bvshell", "bvshell", LocationKind.AllFiles, scope: HiveScope.CurrentUser, keyPath: @"*\shell\bvshell"),
        };

        var item = Assert.Single(SimpleMenuBuilder.Build(entries, MenuScene.Files));

        Assert.Equal(2, item.Sources.Count);
        Assert.Equal(2, item.ToggleRepresentatives().Count());
    }

    [Fact]
    public void Cascade_child_follows_parent_and_reports_hidden_ancestor()
    {
        var parent = Static("static|Machine|*|Encrypt", "加密设置", LocationKind.AllFiles, keyPath: @"*\shell\Encrypt", hasChildren: true);
        var child = Static(
            @"static|Machine|*\shell\Encrypt\shell\Decrypt",
            "解密",
            LocationKind.AllFiles,
            keyPath: @"*\shell\Encrypt\shell\Decrypt",
            parentId: "static|Machine|*|Encrypt",
            indent: 1);

        var items = SimpleMenuBuilder.Build([parent, child], MenuScene.Files);

        Assert.Equal(2, items.Count);
        Assert.Equal("加密设置", items[0].DisplayName);
        Assert.True(items[0].HasChildren);
        Assert.Equal(0, items[0].Indent);
        Assert.Equal("解密", items[1].DisplayName);
        Assert.Equal(1, items[1].Indent);
        Assert.False(items[1].IsAncestorHidden);

        parent.State = EntryState.Disabled;

        var afterHiding = SimpleMenuBuilder.Build([parent, child], MenuScene.Files);

        Assert.True(afterHiding[1].IsAncestorHidden);
        Assert.Equal("父项已隐藏", afterHiding[1].StateText);
        Assert.Equal("子菜单项", afterHiding[1].NoteText);
    }

    [Fact]
    public void Items_are_ordered_like_the_menu()
    {
        var entries = new[]
        {
            Com("{22222222-2222-2222-2222-222222222222}", RegistryViewKind.X64, HiveScope.Machine, LocationKind.AllFiles, "7-Zip"),
            Static("static|Machine|custom", "自定义项", LocationKind.AllFiles),
            Static("static|Machine|system", "打开", LocationKind.AllFiles, isSystem: true),
            ExplorerCommand("static|Machine|new", "baidunetdisk", LocationKind.AllFiles),
        };

        var names = SimpleMenuBuilder.Build(entries, MenuScene.Files).Select(item => item.DisplayName).ToArray();

        Assert.Equal(["打开", "自定义项", "7-Zip", "baidunetdisk"], names);
    }

    [Fact]
    public void Scene_filters_items_by_location()
    {
        var entries = new[]
        {
            Static("static|bg", "Git Bash Here", LocationKind.DirectoryBackground),
            Static("static|file", "edit with notepad++", LocationKind.AllFiles),
            Static("static|objects", "ModernProperties", LocationKind.AllFilesystemObjects),
        };

        var files = SimpleMenuBuilder.Build(entries, MenuScene.Files);
        Assert.Equal(2, files.Count);
        Assert.Contains(files, item => item.DisplayName == "edit with notepad++");
        Assert.Contains(files, item => item.DisplayName == "ModernProperties");

        var background = SimpleMenuBuilder.Build(entries, MenuScene.FolderBackground);
        Assert.Single(background);
        Assert.Equal("Git Bash Here", background[0].DisplayName);

        var allObjects = SimpleMenuBuilder.Build(entries, MenuScene.AllObjects);
        Assert.Single(allObjects);
        Assert.Equal("ModernProperties", allObjects[0].DisplayName);

        Assert.Empty(SimpleMenuBuilder.Build(entries, MenuScene.Drive));
    }

    [Fact]
    public void Hidden_when_all_sources_disabled()
    {
        var entries = new[]
        {
            Com("{33333333-3333-3333-3333-333333333333}", RegistryViewKind.X64, HiveScope.Machine, LocationKind.AllFiles, "7-Zip", EntryState.Disabled),
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
            Com("{44444444-4444-4444-4444-444444444444}", RegistryViewKind.X64, HiveScope.Machine, LocationKind.AllFiles, "Test", EntryState.Disabled),
            Com("{44444444-4444-4444-4444-444444444444}", RegistryViewKind.X86, HiveScope.Machine, LocationKind.AllFiles, "Test"),
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
            Com("{55555555-5555-5555-5555-555555555555}", RegistryViewKind.X64, HiveScope.CurrentUser, LocationKind.AllFiles, "Test"),
            Com("{55555555-5555-5555-5555-555555555555}", RegistryViewKind.X64, HiveScope.Machine, LocationKind.AllFiles, "Test"),
            Com("{55555555-5555-5555-5555-555555555555}", RegistryViewKind.X86, HiveScope.CurrentUser, LocationKind.AllFiles, "Test"),
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
                Id = "orphan|CurrentUser|X64|{66666666-6666-6666-6666-666666666666}",
                DisplayName = "{66666666-6666-6666-6666-666666666666}",
                Kind = EntryKind.ComHandler,
                Scope = HiveScope.CurrentUser,
                View = RegistryViewKind.X64,
                Location = LocationKind.BlockedOrphan,
                RegistryPath = "HKCU\\...\\Blocked",
                KeyPath = string.Empty,
                Clsid = "{66666666-6666-6666-6666-666666666666}",
                State = EntryState.Orphan,
            },
        };

        Assert.Empty(SimpleMenuBuilder.Build(entries, MenuScene.Files));
    }

    private static MenuEntry Static(
        string id,
        string name,
        LocationKind location,
        string? keyPath = null,
        HiveScope scope = HiveScope.Machine,
        string? parentId = null,
        int indent = 0,
        bool hasChildren = false,
        bool isSystem = false) => new()
    {
        Id = id,
        DisplayName = name,
        Kind = EntryKind.StaticVerb,
        Scope = scope,
        View = RegistryViewKind.X64,
        Location = location,
        RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\" + (keyPath ?? id),
        KeyPath = keyPath ?? id,
        ParentId = parentId,
        Indent = indent,
        HasChildren = hasChildren,
        IsSystem = isSystem,
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
        KeyPath = $"key\\{scope}\\{view}\\{location}\\{name}",
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
        KeyPath = "key\\" + id,
    };
}
