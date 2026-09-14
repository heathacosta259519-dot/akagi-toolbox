using ContextMenuEditor.Models;

namespace ContextMenuEditor.Services;

public sealed class MenuReplicaRow
{
    public required MenuProbeItem Source { get; init; }

    public required int Depth { get; init; }

    public required IReadOnlyList<SimpleMenuItem> Owners { get; init; }

    public required string OwnerName { get; init; }

    public string Text => Source.Text;

    public bool IsSeparator => Source.IsSeparator;

    public bool IsEnabled => Source.IsEnabled;

    public SimpleMenuItem? Owner => Owners.Count > 0 ? Owners[0] : null;

    public bool CanToggle => Owners.Any(owner => !owner.IsUnsupported && owner.ToggleRepresentatives().Any());
}

public static class MenuReplicaBuilder
{
    public const string BuiltInOwner = "未识别";

    public static List<MenuReplicaRow> Build(
        MenuProbeResult menu,
        IReadOnlyList<SimpleMenuItem> items,
        IReadOnlyDictionary<string, List<string>> textToClsids)
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
        Append(menu.Items, 0, rows, byText, byClsid, textToClsids, []);
        return rows;
    }

    private static void Append(
        IEnumerable<MenuProbeItem> source,
        int depth,
        List<MenuReplicaRow> rows,
        Dictionary<string, SimpleMenuItem> byText,
        Dictionary<string, SimpleMenuItem> byClsid,
        IReadOnlyDictionary<string, List<string>> textToClsids,
        IReadOnlyList<SimpleMenuItem> parentOwners)
    {
        foreach (var entry in source)
        {
            if (entry.IsSeparator)
            {
                rows.Add(new MenuReplicaRow { Source = entry, Depth = depth, Owners = [], OwnerName = string.Empty });
                continue;
            }

            var owners = Resolve(entry.Text, byText, byClsid, textToClsids);
            if (owners.Count == 0)
            {
                owners = parentOwners;
            }

            rows.Add(new MenuReplicaRow
            {
                Source = entry,
                Depth = depth,
                Owners = owners,
                OwnerName = DescribeOwners(owners),
            });

            if (entry.Children.Count > 0)
            {
                Append(entry.Children, depth + 1, rows, byText, byClsid, textToClsids, owners);
            }
        }
    }

    private static string DescribeOwners(IReadOnlyList<SimpleMenuItem> owners)
    {
        if (owners.Count == 0)
        {
            return BuiltInOwner;
        }

        var names = owners
            .Select(owner => owner.Owner)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.CurrentCultureIgnoreCase);

        var text = string.Join(" / ", names);
        return text.Length == 0 ? BuiltInOwner : text;
    }

    private static IReadOnlyList<SimpleMenuItem> Resolve(
        string text,
        Dictionary<string, SimpleMenuItem> byText,
        Dictionary<string, SimpleMenuItem> byClsid,
        IReadOnlyDictionary<string, List<string>> textToClsids)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        if (byText.TryGetValue(text, out var byName))
        {
            return [byName];
        }

        if (!textToClsids.TryGetValue(text, out var clsids))
        {
            return [];
        }

        var found = new List<SimpleMenuItem>();
        foreach (var clsid in clsids)
        {
            if (byClsid.TryGetValue(clsid, out var item) && !found.Contains(item))
            {
                found.Add(item);
            }
        }

        return found;
    }
}
