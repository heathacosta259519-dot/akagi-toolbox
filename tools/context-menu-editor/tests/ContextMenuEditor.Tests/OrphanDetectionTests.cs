using ContextMenuEditor.Models;
using ContextMenuEditor.Services;
using Microsoft.Win32;
using Xunit;

namespace ContextMenuEditor.Tests;

public sealed class OrphanDetectionTests : IDisposable
{
    private const string OrphanClsid = "{0BADF00D-2222-3333-4444-555566667777}";
    private const string BlockedKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";

    public OrphanDetectionTests()
    {
        using var key = Registry.CurrentUser.CreateSubKey(BlockedKey);
        key!.SetValue(OrphanClsid, string.Empty);
    }

    [Fact]
    public void Blocked_clsid_without_registration_is_reported_as_orphan()
    {
        var entries = new RegistryScanner(new PublisherResolver()).ScanAll();

        var orphan = entries.SingleOrDefault(entry =>
            entry.IsOrphan
            && entry.Clsid == OrphanClsid
            && entry.View == RegistryViewKind.X64
            && entry.Scope == HiveScope.CurrentUser);

        Assert.NotNull(orphan);
        Assert.Equal(LocationKind.BlockedOrphan, orphan!.Location);
        Assert.Equal(EntryState.Orphan, orphan.State);
        Assert.Equal("屏蔽列表中未找到对应注册", orphan.StateDetail);
    }

    public void Dispose()
    {
        using var key = Registry.CurrentUser.OpenSubKey(BlockedKey, writable: true);
        key?.DeleteValue(OrphanClsid, false);
    }
}
