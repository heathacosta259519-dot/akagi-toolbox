using ContextMenuEditor.Services;
using Xunit;

namespace ContextMenuEditor.Tests;

public sealed class EntryNameResolverTests
{
    [Fact]
    public void Handler_key_named_as_clsid_uses_registered_name()
    {
        Assert.Equal("Taskband Pin", EntryNameResolver.ForHandler("{90AA3A4E-1CBA-4233-B8BB-535773D48449}", "Taskband Pin", "Taskband Pin"));
        Assert.Equal("Previous Versions Property Page", EntryNameResolver.ForHandler("{596AB062-B4D2-4215-9F74-E9109B0A8153}", null, "Previous Versions Property Page"));
        Assert.Equal("Portable Devices Menu", EntryNameResolver.ForHandler("{D6791A63-E7E2-4fee-BF52-5DED8E86E9B8}", string.Empty, "Portable Devices Menu"));
    }

    [Fact]
    public void Handler_key_named_as_clsid_without_name_keeps_clsid()
    {
        Assert.Equal("{90AA3A4E-1CBA-4233-B8BB-535773D48449}", EntryNameResolver.ForHandler("{90AA3A4E-1CBA-4233-B8BB-535773D48449}", null, null));
        Assert.Equal("{90AA3A4E-1CBA-4233-B8BB-535773D48449}", EntryNameResolver.ForHandler("{90AA3A4E-1CBA-4233-B8BB-535773D48449}", "  ", "  "));
    }

    [Fact]
    public void Readable_handler_key_wins_over_clsid_value()
    {
        Assert.Equal("WinRAR32", EntryNameResolver.ForHandler("WinRAR32", "{B41DB860-8EE4-11D2-9906-E49FADC173CA}", "WinRAR"));
        Assert.Equal("AABdzCtx", EntryNameResolver.ForHandler("AABdzCtx", "{5B69A6B4-393B-459C-8EBB-214237A9E7AC}", "AABdzCtx Class"));
    }

    [Fact]
    public void Verb_accelerator_markers_are_stripped()
    {
        Assert.Equal("Open Git Bash here", EntryNameResolver.ForVerb("Open Git Ba&sh here", null, "git_shell"));
        Assert.Equal("A&B", EntryNameResolver.ForVerb("A&&B", null, "x"));
    }
}
