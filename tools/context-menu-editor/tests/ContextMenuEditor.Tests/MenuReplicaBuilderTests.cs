using ContextMenuEditor.Models;
using ContextMenuEditor.Services;
using Xunit;

namespace ContextMenuEditor.Tests;

public sealed class MenuReplicaBuilderTests
{
    private const string SevenZipClsid = "{23170F69-40C1-278A-1000-000100020000}";

    [Fact]
    public void Rows_keep_menu_order_depth_and_separators()
    {
        var menu = new MenuProbeResult
        {
            Scene = "Files",
            Target = @"C:\temp\sample.zip",
            Items =
            [
                new MenuProbeItem { Text = "打开(O)" },
                new MenuProbeItem { IsSeparator = true },
                new MenuProbeItem
                {
                    Text = "7-Zip",
                    HasSubmenu = true,
                    Children =
                    [
                        new MenuProbeItem { Text = "解压到 \"sample\\\"", Depth = 1 },
                    ],
                },
                new MenuProbeItem { Text = "复制(C)" },
            ],
        };

        var rows = MenuReplicaBuilder.Build(menu, [], new Dictionary<string, string>());

        Assert.Equal(5, rows.Count);
        Assert.Equal("打开(O)", rows[0].Text);
        Assert.False(rows[0].IsSeparator);
        Assert.True(rows[1].IsSeparator);
        Assert.Equal("7-Zip", rows[2].Text);
        Assert.Equal(0, rows[2].Depth);
        Assert.Equal("解压到 \"sample\\\"", rows[3].Text);
        Assert.Equal(1, rows[3].Depth);
        Assert.Equal("复制(C)", rows[4].Text);
    }

    [Fact]
    public void Static_verbs_are_matched_by_menu_text()
    {
        var menu = new MenuProbeResult
        {
            Items = [new MenuProbeItem { Text = "Open Git Bash here" }],
        };

        var items = new[] { Static("Open Git Bash here", "Git") };

        var row = Assert.Single(MenuReplicaBuilder.Build(menu, items, new Dictionary<string, string>()));

        Assert.NotNull(row.Owner);
        Assert.Equal("Git", row.OwnerName);
        Assert.True(row.CanToggle);
    }

    [Fact]
    public void Handler_items_are_matched_through_the_isolated_probe()
    {
        var menu = new MenuProbeResult
        {
            Items =
            [
                new MenuProbeItem { Text = "7-Zip", HasSubmenu = true },
                new MenuProbeItem { Text = "解压到 \"sample\\\"" },
            ],
        };

        var items = new[] { Com("7-Zip", SevenZipClsid, "7-Zip") };
        var textToClsid = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase)
        {
            ["7-Zip"] = SevenZipClsid,
            ["解压到 \"sample\\\""] = SevenZipClsid,
        };

        var rows = MenuReplicaBuilder.Build(menu, items, textToClsid);

        Assert.NotNull(rows[0].Owner);
        Assert.Equal("7-Zip", rows[0].OwnerName);
        Assert.NotNull(rows[1].Owner);
        Assert.True(rows[1].CanToggle);
    }

    [Fact]
    public void Submenu_children_inherit_the_parent_owner()
    {
        var menu = new MenuProbeResult
        {
            Items =
            [
                new MenuProbeItem
                {
                    Text = "BandiView",
                    HasSubmenu = true,
                    Children =
                    [
                        new MenuProbeItem { Text = "用 BandiView 浏览(3)", Depth = 1 },
                        new MenuProbeItem { Text = "用 BandiView 转换(4)", Depth = 1 },
                    ],
                },
            ],
        };

        var items = new[] { Com("BandiView", "{0002DEAD-9BF7-4CFA-8A5C-DE8679340001}", "BandiView") };
        var textToClsid = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase)
        {
            ["BandiView"] = "{0002DEAD-9BF7-4CFA-8A5C-DE8679340001}",
        };

        var rows = MenuReplicaBuilder.Build(menu, items, textToClsid);

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.NotNull(row.Owner);
            Assert.Equal("BandiView", row.OwnerName);
            Assert.True(row.CanToggle);
        });
    }

    [Fact]
    public void Built_in_items_have_no_owner_and_cannot_be_toggled()
    {
        var menu = new MenuProbeResult
        {
            Items = [new MenuProbeItem { Text = "复制(C)" }, new MenuProbeItem { Text = "属性(R)", IsEnabled = false }],
        };

        var rows = MenuReplicaBuilder.Build(menu, [], new Dictionary<string, string>());

        Assert.Null(rows[0].Owner);
        Assert.Equal(MenuReplicaBuilder.BuiltInOwner, rows[0].OwnerName);
        Assert.False(rows[0].CanToggle);
        Assert.False(rows[1].IsEnabled);
    }

    private static SimpleMenuItem Static(string name, string owner) => new()
    {
        Id = "verb|" + name,
        DisplayName = name,
        Kind = EntryKind.StaticVerb,
        Owner = owner,
        Sources =
        [
            new MenuEntry
            {
                Id = "static|Machine|x|" + name,
                DisplayName = name,
                Kind = EntryKind.StaticVerb,
                Scope = HiveScope.Machine,
                View = RegistryViewKind.X64,
                Location = LocationKind.AllFiles,
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\*\shell\x",
                KeyPath = @"*\shell\x",
                Publisher = owner,
            },
        ],
    };

    private static SimpleMenuItem Com(string name, string clsid, string owner) => new()
    {
        Id = "com|" + clsid,
        DisplayName = name,
        Kind = EntryKind.ComHandler,
        Owner = owner,
        Sources =
        [
            new MenuEntry
            {
                Id = "handler|Machine|X64|*|" + name,
                DisplayName = name,
                Kind = EntryKind.ComHandler,
                Scope = HiveScope.Machine,
                View = RegistryViewKind.X64,
                Location = LocationKind.AllFiles,
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\*\shellex\ContextMenuHandlers\" + name,
                KeyPath = @"*\shellex\ContextMenuHandlers\" + name,
                Clsid = clsid,
                Publisher = owner,
            },
        ],
    };
}
