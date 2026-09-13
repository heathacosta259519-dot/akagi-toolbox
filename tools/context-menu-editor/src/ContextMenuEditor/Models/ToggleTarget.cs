namespace ContextMenuEditor.Models;

public sealed record ToggleTarget(
    EntryKind Kind,
    HiveScope Scope,
    RegistryViewKind View,
    string KeyPath,
    string? Clsid,
    string DisplayName);
