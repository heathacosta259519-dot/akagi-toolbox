namespace ContextMenuEditor.Models;

public enum MenuScene
{
    Files,
    Folders,
    FolderBackground,
    DesktopBackground,
    Drive,
}

public static class MenuScenes
{
    public static readonly IReadOnlyList<MenuScene> All =
    [
        MenuScene.Files,
        MenuScene.Folders,
        MenuScene.FolderBackground,
        MenuScene.DesktopBackground,
        MenuScene.Drive,
    ];

    public static string DisplayName(this MenuScene scene) => scene switch
    {
        MenuScene.Files => "文件右键",
        MenuScene.Folders => "文件夹右键",
        MenuScene.FolderBackground => "文件夹空白处",
        MenuScene.DesktopBackground => "桌面空白处",
        _ => "驱动器右键",
    };

    public static IReadOnlyList<LocationKind> Locations(this MenuScene scene) => scene switch
    {
        MenuScene.Files => [LocationKind.AllFiles, LocationKind.AllFilesystemObjects],
        MenuScene.Folders => [LocationKind.Directory, LocationKind.Folder, LocationKind.AllFilesystemObjects],
        MenuScene.FolderBackground => [LocationKind.DirectoryBackground],
        MenuScene.DesktopBackground => [LocationKind.DesktopBackground],
        _ => [LocationKind.Drive],
    };
}
