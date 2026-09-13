namespace ContextMenuEditor.Models;

public sealed class SimpleMenuItem
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required EntryKind Kind { get; init; }
    public required IReadOnlyList<MenuEntry> Sources { get; init; }
    public string? Publisher { get; init; }
    public string? ParentId { get; init; }
    public int Indent { get; init; }
    public bool HasChildren { get; init; }
    public bool IsSystem { get; init; }
    public bool IsUnsupported { get; init; }
    public bool IsAncestorHidden { get; set; }

    public bool IsShown => Sources.Any(source => source.State == EntryState.Enabled);

    public bool IsPartiallyHidden =>
        IsShown && Sources.Any(source => source.State != EntryState.Enabled);

    public string StateText => IsUnsupported
        ? "暂不支持"
        : !IsShown
            ? "已隐藏"
            : IsAncestorHidden
                ? "父项已隐藏"
                : IsPartiallyHidden
                    ? "部分隐藏"
                    : "显示中";

    public string NoteText => IsUnsupported
        ? "新式菜单项，暂不支持隐藏"
        : Indent > 0
            ? "子菜单项"
            : IsSystem
                ? "系统关键项"
                : Kind == EntryKind.ComHandler
                    ? "COM 扩展"
                    : "静态菜单项";

    public IEnumerable<MenuEntry> ToggleRepresentatives()
    {
        var toggleable = Sources.Where(source => source.CanToggle).ToArray();

        if (Kind == EntryKind.ComHandler && toggleable.All(source => source.Clsid != null))
        {
            return toggleable
                .GroupBy(source => source.View)
                .Select(group => group.OrderBy(source => source.Scope == HiveScope.Machine ? 0 : 1).First());
        }

        return toggleable
            .GroupBy(source => (source.Scope, source.View))
            .Select(group => group.First());
    }
}
