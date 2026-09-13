using ContextMenuEditor.Models;
using ContextMenuEditor.Services;
using Microsoft.Win32;
using Xunit;

namespace ContextMenuEditor.Tests;

public sealed class ScannerSandboxTests : IDisposable
{
    private const string VerbKey = @"SOFTWARE\Classes\Directory\Background\shell\AkagiToolboxTestVerb";
    private const string CascadeKey = @"SOFTWARE\Classes\Directory\Background\shell\AkagiToolboxTestCascade";
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

        using (var cascade = Registry.CurrentUser.CreateSubKey(CascadeKey))
        {
            cascade.SetValue("MUIVerb", "Akagi 测试级联");
            cascade.SetValue("ExtendedSubCommandsKey", @"Directory\Background\shell\AkagiToolboxTestCascade");
            using var child = cascade.CreateSubKey(@"shell\AkagiToolboxTestChild");
            child.SetValue("MUIVerb", "Akagi 子菜单项");
            using var childCommand = child.CreateSubKey("command");
            childCommand.SetValue(null, @"""C:\Windows\System32\notepad.exe"" ""%1""");
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

    [Fact]
    public void Scan_expands_cascade_submenu_children()
    {
        var entries = new RegistryScanner(new PublisherResolver()).ScanAll();

        var parent = entries.Single(entry => entry.Id == @"static|CurrentUser|Directory\Background|AkagiToolboxTestCascade");
        Assert.True(parent.HasChildren);
        Assert.Equal(0, parent.Indent);
        Assert.Null(parent.ParentId);

        var child = entries.Single(entry => entry.Id == @"static|CurrentUser|Directory\Background\shell\AkagiToolboxTestCascade\shell\AkagiToolboxTestChild");
        Assert.Equal("Akagi 子菜单项", child.DisplayName);
        Assert.Equal(1, child.Indent);
        Assert.Equal(parent.Id, child.ParentId);
        Assert.Equal(@"Directory\Background\shell\AkagiToolboxTestCascade\shell\AkagiToolboxTestChild", child.KeyPath);
        Assert.Equal(@"HKEY_CURRENT_USER\SOFTWARE\Classes\Directory\Background\shell\AkagiToolboxTestCascade\shell\AkagiToolboxTestChild", child.RegistryPath);
        Assert.Equal(LocationKind.DirectoryBackground, child.Location);
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(VerbKey, false);
        Registry.CurrentUser.DeleteSubKeyTree(CascadeKey, false);
        Registry.CurrentUser.DeleteSubKeyTree(HandlerKey, false);
    }
}
