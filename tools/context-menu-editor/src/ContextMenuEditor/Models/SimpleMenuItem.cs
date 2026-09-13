namespace ContextMenuEditor.Models;

public sealed class SimpleMenuItem
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required EntryKind Kind { get; init; }
    public required IReadOnlyList<MenuEntry> Sources { get; init; }
    public string? Publisher { get; init; }
    public bool IsSystem { get; init; }
    public bool IsUnsupported { get; init; }

    public bool IsShown => Sources.Any(source => source.State == EntryState.Enabled);

    public bool IsPartiallyHidden =>
        IsShown && Sources.Any(source => source.State != EntryState.Enabled);

    public string StateText => IsUnsupported
        ? "暂不支持"
        : !IsShown
            ? "已隐藏"
            : IsPartiallyHidden
                ? "部分隐藏"
                : "显示中";

    public string NoteText => IsUnsupported
        ? "新式菜单项，暂不支持隐藏"
        : IsSystem
            ? "系统关键项"
            : Kind == EntryKind.ComHandler
                ? "COM 扩展"
                : "静态菜单项";

    public IEnumerable<MenuEntry> ToggleRepresentatives() =>
        Sources
            .Where(source => source.CanToggle)
            .GroupBy(source => source.View)
            .Select(group => group.OrderBy(source => source.Scope == HiveScope.Machine ? 0 : 1).First());
}
