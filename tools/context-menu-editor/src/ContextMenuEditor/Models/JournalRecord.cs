namespace ContextMenuEditor.Models;

public sealed class JournalRecord
{
    public DateTimeOffset Time { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? KeyPath { get; set; }
    public string? Clsid { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string View { get; set; } = string.Empty;

    public static JournalRecord FromToggle(ToggleTarget target, string action) => new()
    {
        Time = DateTimeOffset.Now,
        Action = action,
        Kind = target.Kind.ToString(),
        Target = target.DisplayName,
        KeyPath = target.KeyPath,
        Clsid = target.Clsid,
        Scope = target.Scope.ToString(),
        View = target.View.ToString(),
    };

    public static JournalRecord ForClassicMenu(string action) => new()
    {
        Time = DateTimeOffset.Now,
        Action = action,
        Kind = "classic-menu",
        Target = "Windows 11 经典右键菜单",
    };

    public ToggleTarget ToTarget() => new(
        Enum.TryParse<EntryKind>(Kind, out var kind) ? kind : EntryKind.StaticVerb,
        Enum.TryParse<HiveScope>(Scope, out var scope) ? scope : HiveScope.CurrentUser,
        Enum.TryParse<RegistryViewKind>(View, out var view) ? view : RegistryViewKind.X64,
        KeyPath ?? string.Empty,
        Clsid,
        Target);

    public string ActionText => Action switch
    {
        "disable" => "禁用",
        "enable" => "恢复",
        "clean" => "清理残留",
        "classic-on" => "开启经典菜单",
        "classic-off" => "关闭经典菜单",
        _ => Action,
    };
}
