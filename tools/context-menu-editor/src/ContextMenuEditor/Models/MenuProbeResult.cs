namespace ContextMenuEditor.Models;

public sealed class MenuProbeItem
{
    public int Depth { get; set; }

    public string Text { get; set; } = string.Empty;

    public string? Verb { get; set; }

    public uint CommandId { get; set; }

    public bool IsSeparator { get; set; }

    public bool IsOwnerDraw { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool HasSubmenu { get; set; }

    public List<MenuProbeItem> Children { get; set; } = [];
}

public sealed class MenuProbeResult
{
    public string Scene { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public List<MenuProbeItem> Items { get; set; } = [];

    public string? Error { get; set; }
}
