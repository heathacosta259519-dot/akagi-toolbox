namespace ContextMenuEditor.Models;

public enum EntryKind
{
    StaticVerb,
    ComHandler,
    ExplorerCommand,
}

public enum HiveScope
{
    Machine,
    CurrentUser,
}

public enum RegistryViewKind
{
    X64,
    X86,
}

public enum EntryState
{
    Enabled,
    Disabled,
    Orphan,
}

public enum LocationKind
{
    AllFiles,
    Directory,
    DirectoryBackground,
    Drive,
    DesktopBackground,
    AllFilesystemObjects,
    Folder,
    BlockedOrphan,
}
