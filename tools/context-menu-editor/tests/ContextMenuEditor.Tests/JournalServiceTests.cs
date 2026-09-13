using ContextMenuEditor.Models;
using ContextMenuEditor.Services;
using Xunit;

namespace ContextMenuEditor.Tests;

public sealed class JournalServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        "context-menu-editor-journal-" + Guid.NewGuid().ToString("N") + ".ndjson");

    [Fact]
    public void Append_and_read_round_trip()
    {
        var journal = new JournalService(_path);
        var target = new ToggleTarget(
            EntryKind.StaticVerb,
            HiveScope.CurrentUser,
            RegistryViewKind.X64,
            @"Directory\Background\shell\AkagiTestVerb",
            null,
            "测试项");

        journal.Append(JournalRecord.FromToggle(target, "disable"));

        var records = journal.ReadAll();
        Assert.Single(records);
        Assert.Equal("disable", records[0].Action);
        Assert.Equal("测试项", records[0].Target);
        Assert.Equal(@"Directory\Background\shell\AkagiTestVerb", records[0].KeyPath);

        var restored = records[0].ToTarget();
        Assert.Equal(EntryKind.StaticVerb, restored.Kind);
        Assert.Equal(HiveScope.CurrentUser, restored.Scope);
        Assert.Equal(RegistryViewKind.X64, restored.View);
    }

    [Fact]
    public void Classic_menu_record_reverses_action_text()
    {
        var journal = new JournalService(_path);
        journal.Append(JournalRecord.ForClassicMenu("classic-on"));

        var record = journal.ReadAll()[0];
        Assert.Equal("开启经典菜单", record.ActionText);
        Assert.Equal("classic-menu", record.Kind);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
