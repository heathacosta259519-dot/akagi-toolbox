namespace ContextMenuEditor.Models;

public sealed record LocationDefinition(
    LocationKind Kind,
    string SubPath,
    string DisplayName,
    bool HasShell,
    bool HasShellex);

public static class RegistryLocations
{
    public static readonly IReadOnlyList<LocationDefinition> All =
    [
        new(LocationKind.AllFiles, "*", "所有文件", true, true),
        new(LocationKind.Directory, "Directory", "文件夹", true, true),
        new(LocationKind.DirectoryBackground, @"Directory\Background", "文件夹背景", true, true),
        new(LocationKind.Drive, "Drive", "驱动器", true, true),
        new(LocationKind.DesktopBackground, "DesktopBackground", "桌面背景", true, false),
        new(LocationKind.AllFilesystemObjects, "AllFilesystemObjects", "所有文件系统对象", true, true),
        new(LocationKind.Folder, "Folder", "文件夹（通用）", true, true),
    ];

    private static readonly HashSet<string> ProtectedFolderVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "open",
        "explore",
        "opennewtab",
        "opennewwindow",
        "opennewprocess",
    };

    public static string DisplayNameFor(LocationKind kind) =>
        kind == LocationKind.BlockedOrphan
            ? "已屏蔽残留"
            : All.First(location => location.Kind == kind).DisplayName;

    public static bool IsSystemItem(LocationKind location, string verbName) =>
        location == LocationKind.Folder && ProtectedFolderVerbs.Contains(verbName);
}
