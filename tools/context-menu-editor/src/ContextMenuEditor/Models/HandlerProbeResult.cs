namespace ContextMenuEditor.Models;

public sealed class HandlerProbeEntry
{
    public string Clsid { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<string> Texts { get; set; } = [];

    public string? Error { get; set; }
}

public sealed class HandlerProbeResult
{
    public List<HandlerProbeEntry> Handlers { get; set; } = [];
}
