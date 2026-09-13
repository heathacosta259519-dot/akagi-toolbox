namespace ContextMenuEditor.Models;

public sealed class MenuEntry
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required EntryKind Kind { get; init; }
    public required HiveScope Scope { get; init; }
    public required RegistryViewKind View { get; init; }
    public required LocationKind Location { get; init; }
    public required string RegistryPath { get; init; }
    public required string KeyPath { get; init; }
    public string? Command { get; init; }
    public string? Clsid { get; init; }
    public string? IconSource { get; init; }
    public string? Publisher { get; init; }
    public bool IsSystem { get; init; }
    public EntryState State { get; set; } = EntryState.Enabled;
    public string? StateDetail { get; set; }

    public bool IsOrphan => State == EntryState.Orphan;

    public ToggleTarget ToToggleTarget() => new(Kind, Scope, View, KeyPath, Clsid, DisplayName);

    public bool CanToggle => Kind switch
    {
        EntryKind.ExplorerCommand => false,
        EntryKind.ComHandler => Clsid != null,
        _ => true,
    };

    public string KindText => Kind switch
    {
        EntryKind.StaticVerb => "静态项",
        EntryKind.ComHandler => "COM 扩展",
        _ => "新式命令",
    };

    public string ScopeText
    {
        get
        {
            var text = Scope == HiveScope.Machine ? "本机" : "当前用户";
            return View == RegistryViewKind.X86 ? text + " (32位)" : text;
        }
    }

    public string StateText => State switch
    {
        EntryState.Enabled => CanToggle ? "已启用" : "暂不支持",
        EntryState.Disabled => "已禁用",
        _ => "残留屏蔽",
    };
}
