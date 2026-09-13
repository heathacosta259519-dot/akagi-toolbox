namespace ContextMenuEditor.Models;

public enum MenuScene
{
    Files,
    Folders,
    FolderBackground,
    DesktopBackground,
    Drive,
    AllObjects,
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
        MenuScene.AllObjects,
    ];

    public static string DisplayName(this MenuScene scene) => scene switch
    {
        MenuScene.Files => "文件",
        MenuScene.Folders => "文件夹",
        MenuScene.FolderBackground => "文件夹空白处",
        MenuScene.DesktopBackground => "桌面空白处",
        MenuScene.Drive => "驱动器",
        _ => "所有对象",
    };

    public static IReadOnlyList<LocationKind> Locations(this MenuScene scene) => scene switch
    {
        MenuScene.Files => [LocationKind.AllFiles, LocationKind.AllFilesystemObjects],
        MenuScene.Folders => [LocationKind.Directory, LocationKind.Folder, LocationKind.AllFilesystemObjects],
        MenuScene.FolderBackground => [LocationKind.DirectoryBackground],
        MenuScene.DesktopBackground => [LocationKind.DesktopBackground],
        MenuScene.Drive => [LocationKind.Drive],
        _ => [LocationKind.AllFilesystemObjects],
    };
}
