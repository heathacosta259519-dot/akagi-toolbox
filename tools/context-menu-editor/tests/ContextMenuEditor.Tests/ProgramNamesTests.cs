using ContextMenuEditor.Services;
using Xunit;

namespace ContextMenuEditor.Tests;

public sealed class ProgramNamesTests
{
    private static readonly string ProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    private static readonly string ProgramFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string Roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string Windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    [Fact]
    public void Product_folder_under_program_files_is_the_program_name()
    {
        Assert.Equal("Bandizip", ProgramNames.FromPath(Path.Combine(ProgramFiles, "Bandizip", "bdzshl64.dll")));
        Assert.Equal("7-Zip", ProgramNames.FromPath(Path.Combine(ProgramFiles, "7-Zip", "7-zip.dll")));
        Assert.Equal("PowerToys", ProgramNames.FromPath(Path.Combine(ProgramFiles, "PowerToys", "PowerToys.PowerRenameContextMenu.dll")));
    }

    [Fact]
    public void Common_files_is_skipped_for_the_vendor_folder()
    {
        var path = Path.Combine(ProgramFilesX86, "Common Files", "Adobe", "CoreSyncExtension", "CoreSync_x64.dll");
        Assert.Equal("Adobe", ProgramNames.FromPath(path));
    }

    [Fact]
    public void Per_user_programs_folder_resolves_too()
    {
        var path = Path.Combine(LocalAppData, "Programs", "WeiyunApp", "resources", "native", "DiskMenuShell64.dll");
        Assert.Equal("WeiyunApp", ProgramNames.FromPath(path));

        var quark = Path.Combine(LocalAppData, "Programs", "Common", "Quark", "quarkshellext.dll");
        Assert.Equal("Quark", ProgramNames.FromPath(quark));

        Assert.Equal("Tencent", ProgramNames.FromPath(Path.Combine(Roaming, "Tencent", "x.dll")));
    }

    [Fact]
    public void System_files_report_windows()
    {
        Assert.Equal("Windows", ProgramNames.FromPath(Path.Combine(Windows, "System32", "shell32.dll")));
        Assert.Equal("Windows", ProgramNames.FromPath(Path.Combine(Windows, "system32", "WorkfoldersShell.dll")));
    }

    [Fact]
    public void Windows_defender_keeps_its_product_folder_name()
    {
        Assert.Equal("Windows Defender", ProgramNames.FromPath(Path.Combine(ProgramFiles, "Windows Defender", "shellext.dll")));
    }

    [Fact]
    public void Unknown_paths_return_null()
    {
        Assert.Null(ProgramNames.FromPath(null));
        Assert.Null(ProgramNames.FromPath("   "));
        Assert.Null(ProgramNames.FromPath(@"D:\Somewhere\Else\tool.dll"));
    }
}
