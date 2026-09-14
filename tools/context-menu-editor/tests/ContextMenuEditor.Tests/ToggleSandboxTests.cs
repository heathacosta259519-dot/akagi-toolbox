using ContextMenuEditor.Models;
using ContextMenuEditor.Services;
using Microsoft.Win32;
using Xunit;

namespace ContextMenuEditor.Tests;

public sealed class ToggleSandboxTests : IDisposable
{
    private const string VerbSubPath = @"Directory\Background\shell\AkagiToolboxToggleVerb";
    private const string VerbKey = @"SOFTWARE\Classes\" + VerbSubPath;
    private const string HandlerSubPath = @"*\shellex\ContextMenuHandlers\AkagiToolboxToggleHandler";
    private const string HandlerKey = @"SOFTWARE\Classes\" + HandlerSubPath;
    private const string CommandVerbSubPath = @"*\shell\AkagiToolboxExplorerCommand";
    private const string CommandVerbKey = @"SOFTWARE\Classes\" + CommandVerbSubPath;
    private const string BlockedKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";
    private const string TestClsid = "{FACEB00C-1111-2222-3333-444455556666}";

    private readonly ToggleService _toggles = new();

    public ToggleSandboxTests()
    {
        using (var verb = Registry.CurrentUser.CreateSubKey(VerbKey))
        {
            using var command = verb.CreateSubKey("command");
            command.SetValue(null, @"""C:\Windows\System32\notepad.exe""");
        }

        using (var handler = Registry.CurrentUser.CreateSubKey(HandlerKey))
        {
            handler.SetValue(null, TestClsid);
        }

        using (var commandVerb = Registry.CurrentUser.CreateSubKey(CommandVerbKey))
        {
            commandVerb.SetValue("MUIVerb", "Akagi 新式菜单项");
            commandVerb.SetValue("ExplorerCommandHandler", "{0002DEAD-9BF7-4CFA-8A5C-DE8679340001}");
        }
    }

    [Fact]
    public void Static_verb_disable_and_enable_round_trip()
    {
        var target = new ToggleTarget(EntryKind.StaticVerb, HiveScope.CurrentUser, RegistryViewKind.X64, VerbSubPath, null, "Akagi 测试项");

        _toggles.Disable(target);

        using (var key = Registry.CurrentUser.OpenSubKey(VerbKey))
        {
            Assert.NotNull(key);
            Assert.NotNull(key!.GetValue("LegacyDisable"));
        }

        var entry = new RegistryScanner(new PublisherResolver()).ScanAll()
            .Single(item => item.Id == @"static|CurrentUser|Directory\Background|AkagiToolboxToggleVerb");
        Assert.Equal(EntryState.Disabled, entry.State);

        _toggles.Enable(target);

        using (var key = Registry.CurrentUser.OpenSubKey(VerbKey))
        {
            Assert.NotNull(key);
            Assert.Null(key!.GetValue("LegacyDisable"));
        }
    }

    [Fact]
    public void Com_handler_disable_and_enable_round_trip()
    {
        var target = new ToggleTarget(EntryKind.ComHandler, HiveScope.CurrentUser, RegistryViewKind.X64, HandlerSubPath, TestClsid, "Akagi 测试处理器");

        _toggles.Disable(target);

        using (var blocked = Registry.CurrentUser.OpenSubKey(BlockedKey))
        {
            Assert.NotNull(blocked);
            Assert.Contains(TestClsid, blocked!.GetValueNames(), StringComparer.OrdinalIgnoreCase);
        }

        var entry = new RegistryScanner(new PublisherResolver()).ScanAll()
            .Single(item => item.Id == "handler|CurrentUser|X64|*|AkagiToolboxToggleHandler");
        Assert.Equal(EntryState.Disabled, entry.State);

        _toggles.Enable(target);

        using (var blocked = Registry.CurrentUser.OpenSubKey(BlockedKey))
        {
            var names = blocked?.GetValueNames() ?? [];
            Assert.DoesNotContain(TestClsid, names, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Explorer_command_verb_disable_and_enable_round_trip()
    {
        var target = new ToggleTarget(EntryKind.ExplorerCommand, HiveScope.CurrentUser, RegistryViewKind.X64, CommandVerbSubPath, null, "Akagi 新式菜单项");

        _toggles.Disable(target);

        using (var key = Registry.CurrentUser.OpenSubKey(CommandVerbKey))
        {
            Assert.NotNull(key);
            Assert.NotNull(key!.GetValue("LegacyDisable"));
        }

        var entry = new RegistryScanner(new PublisherResolver()).ScanAll()
            .Single(item => item.Id == @"static|CurrentUser|*|AkagiToolboxExplorerCommand");
        Assert.Equal(EntryKind.ExplorerCommand, entry.Kind);
        Assert.True(entry.CanToggle);
        Assert.Equal(EntryState.Disabled, entry.State);

        _toggles.Enable(target);

        using (var key = Registry.CurrentUser.OpenSubKey(CommandVerbKey))
        {
            Assert.NotNull(key);
            Assert.Null(key!.GetValue("LegacyDisable"));
        }
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(VerbKey, false);
        Registry.CurrentUser.DeleteSubKeyTree(HandlerKey, false);
        Registry.CurrentUser.DeleteSubKeyTree(CommandVerbKey, false);
        using var blocked = Registry.CurrentUser.OpenSubKey(BlockedKey, writable: true);
        blocked?.DeleteValue(TestClsid, false);
    }
}
