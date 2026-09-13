using ContextMenuEditor.Services;
using Xunit;

namespace ContextMenuEditor.Tests;

public sealed class ParsingTests
{
    [Fact]
    public void Normalize_adds_braces_and_uppercases()
    {
        Assert.Equal("{DEADBEEF-1234-5678-9ABC-DEF012345678}", ClsidNormalizer.Normalize("deadbeef-1234-5678-9abc-def012345678"));
        Assert.Equal("{DEADBEEF-1234-5678-9ABC-DEF012345678}", ClsidNormalizer.Normalize("{deadbeef-1234-5678-9abc-def012345678}"));
        Assert.Null(ClsidNormalizer.Normalize("not-a-clsid"));
        Assert.Null(ClsidNormalizer.Normalize(null));
    }

    [Fact]
    public void Verb_name_prefers_muiverb_then_default_then_key_name()
    {
        Assert.Equal("菜单文字", EntryNameResolver.ForVerb("菜单文字", "默认值", "keyname"));
        Assert.Equal("默认值", EntryNameResolver.ForVerb(null, "默认值", "keyname"));
        Assert.Equal("keyname", EntryNameResolver.ForVerb(null, null, "keyname"));
    }

    [Fact]
    public void Verb_name_strips_accelerator_markers()
    {
        Assert.Equal("Open Git Bash here", EntryNameResolver.Clean("Open Git Ba&sh here"));
        Assert.Equal("A&B", EntryNameResolver.Clean("A&&B"));
    }

    [Fact]
    public void Executable_extraction_handles_quotes_and_arguments()
    {
        Assert.Equal(@"C:\Program Files\Git\git-bash.exe", ShellPathParser.ExtractExecutable(@"""C:\Program Files\Git\git-bash.exe"" ""--cd=%v."""));
        Assert.Equal(@"C:\Windows\System32\cmd.exe", ShellPathParser.ExtractExecutable(@"C:\Windows\System32\cmd.exe /s /k"));
        Assert.Null(ShellPathParser.ExtractExecutable(null));
    }

    [Fact]
    public void Icon_value_parsing_extracts_path_and_index()
    {
        var parsed = ShellPathParser.ParseIconValue(@"C:\Program Files\Git\git-bash.exe,0");
        Assert.NotNull(parsed);
        Assert.Equal(@"C:\Program Files\Git\git-bash.exe", parsed!.Value.Path);
        Assert.Equal(0, parsed.Value.Index);

        var indirect = ShellPathParser.ParseIconValue(@"%SystemRoot%\System32\shell32.dll,-8506");
        Assert.NotNull(indirect);
        Assert.Equal(-8506, indirect!.Value.Index);
        Assert.EndsWith(@"System32\shell32.dll", indirect.Value.Path, StringComparison.OrdinalIgnoreCase);
    }
}
