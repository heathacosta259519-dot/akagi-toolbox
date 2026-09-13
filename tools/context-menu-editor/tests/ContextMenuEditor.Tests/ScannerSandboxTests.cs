using ContextMenuEditor.Models;
using ContextMenuEditor.Services;
using Microsoft.Win32;
using Xunit;

namespace ContextMenuEditor.Tests;

public sealed class ScannerSandboxTests : IDisposable
{
    private const string VerbKey = @"SOFTWARE\Classes\Directory\Background\shell\AkagiToolboxTestVerb";
    private const string HandlerKey = @"SOFTWARE\Classes\*\shellex\ContextMenuHandlers\AkagiToolboxTestHandler";
    private const string TestClsid = "{DEADBEEF-1234-5678-9ABC-DEF012345678}";

    public ScannerSandboxTests()
    {
        using (var verb = Registry.CurrentUser.CreateSubKey(VerbKey))
        {
            verb.SetValue("MUIVerb", "Akagi 测试菜单项");
            using var command = verb.CreateSubKey("command");
            command.SetValue(null, @"""C:\Windows\System32\notepad.exe"" ""%1""");
        }

        using (var handler = Registry.CurrentUser.CreateSubKey(HandlerKey))
        {
            handler.SetValue(null, TestClsid);
        }
    }

    [Fact]
    public void Scan_finds_sandbox_entries()
    {
        var entries = new RegistryScanner(new PublisherResolver()).ScanAll();

        var verb = entries.SingleOrDefault(entry => entry.Id == @"static|CurrentUser|Directory\Background|AkagiToolboxTestVerb");
        Assert.NotNull(verb);
        Assert.Equal("Akagi 测试菜单项", verb!.DisplayName);
        Assert.Equal(EntryState.Enabled, verb.State);
        Assert.Equal(LocationKind.DirectoryBackground, verb.Location);
        Assert.Equal(@"HKEY_CURRENT_USER\SOFTWARE\Classes\Directory\Background\shell\AkagiToolboxTestVerb", verb.RegistryPath);

        var handler = entries.SingleOrDefault(entry => entry.Id == "handler|CurrentUser|X64|*|AkagiToolboxTestHandler");
        Assert.NotNull(handler);
        Assert.Equal(TestClsid, handler!.Clsid);
        Assert.Equal(EntryState.Enabled, handler.State);
        Assert.Equal(EntryKind.ComHandler, handler.Kind);
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(VerbKey, false);
        Registry.CurrentUser.DeleteSubKeyTree(HandlerKey, false);
    }
}
