using ContextMenuEditor.Models;

namespace ContextMenuEditor.Services;

public sealed class MenuReplicaRow
{
    public required MenuProbeItem Source { get; init; }

    public required int Depth { get; init; }

    public SimpleMenuItem? Owner { get; init; }

    public required string OwnerName { get; init; }

    public string Text => Source.Text;

    public bool IsSeparator => Source.IsSeparator;

    public bool IsEnabled => Source.IsEnabled;

    public bool CanToggle => Owner is { IsUnsupported: false } && Owner.ToggleRepresentatives().Any();
}

public static class MenuReplicaBuilder
{
    public const string BuiltInOwner = "未识别";

    public static List<MenuReplicaRow> Build(
        MenuProbeResult menu,
        IReadOnlyList<SimpleMenuItem> items,
        IReadOnlyDictionary<string, string> textToClsid)
    {
        var byText = new Dictionary<string, SimpleMenuItem>(StringComparer.CurrentCultureIgnoreCase);
        var byClsid = new Dictionary<string, SimpleMenuItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.DisplayName))
            {
                byText.TryAdd(item.DisplayName, item);
            }

            foreach (var clsid in item.Sources.Select(source => source.Clsid).Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                byClsid.TryAdd(clsid!, item);
            }
        }

        var rows = new List<MenuReplicaRow>();
        Append(menu.Items, 0, rows, byText, byClsid, textToClsid);
        return rows;
    }

    private static void Append(
        IEnumerable<MenuProbeItem> source,
        int depth,
        List<MenuReplicaRow> rows,
        Dictionary<string, SimpleMenuItem> byText,
        Dictionary<string, SimpleMenuItem> byClsid,
        IReadOnlyDictionary<string, string> textToClsid)
    {
        foreach (var entry in source)
        {
            if (entry.IsSeparator)
            {
                rows.Add(new MenuReplicaRow { Source = entry, Depth = depth, OwnerName = string.Empty });
                continue;
            }

            var owner = Resolve(entry.Text, byText, byClsid, textToClsid);
            rows.Add(new MenuReplicaRow
            {
                Source = entry,
                Depth = depth,
                Owner = owner,
                OwnerName = owner?.Owner ?? BuiltInOwner,
            });

            if (entry.Children.Count > 0)
            {
                Append(entry.Children, depth + 1, rows, byText, byClsid, textToClsid);
            }
        }
    }

    private static SimpleMenuItem? Resolve(
        string text,
        Dictionary<string, SimpleMenuItem> byText,
        Dictionary<string, SimpleMenuItem> byClsid,
        IReadOnlyDictionary<string, string> textToClsid)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (byText.TryGetValue(text, out var byName))
        {
            return byName;
        }

        if (textToClsid.TryGetValue(text, out var clsid) && byClsid.TryGetValue(clsid, out var byHandler))
        {
            return byHandler;
        }

        return null;
    }
}
