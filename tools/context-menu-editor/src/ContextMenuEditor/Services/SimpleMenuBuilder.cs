using ContextMenuEditor.Models;

namespace ContextMenuEditor.Services;

public static class SimpleMenuBuilder
{
    public static List<SimpleMenuItem> Build(IEnumerable<MenuEntry> entries, MenuScene scene)
    {
        var locations = scene.Locations().ToHashSet();
        var relevant = entries
            .Where(entry => !entry.IsOrphan && locations.Contains(entry.Location))
            .ToArray();

        var groups = new List<List<MenuEntry>>();
        var byClsid = new Dictionary<string, List<MenuEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in relevant)
        {
            if (entry.Kind == EntryKind.ComHandler && entry.Clsid != null)
            {
                if (!byClsid.TryGetValue(entry.Clsid, out var group))
                {
                    group = [];
                    byClsid[entry.Clsid] = group;
                }

                group.Add(entry);
            }
            else
            {
                groups.Add([entry]);
            }
        }

        groups.AddRange(byClsid.Values);

        return groups
            .Select(CreateItem)
            .OrderBy(TierOf)
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static SimpleMenuItem CreateItem(List<MenuEntry> sources)
    {
        var primary = sources[0];
        var isComHandler = primary.Kind == EntryKind.ComHandler && primary.Clsid != null;

        return new SimpleMenuItem
        {
            Id = isComHandler ? "com|" + primary.Clsid : primary.Id,
            DisplayName = primary.DisplayName,
            Kind = primary.Kind,
            Sources = sources,
            Publisher = sources.Select(source => source.Publisher).FirstOrDefault(publisher => !string.IsNullOrWhiteSpace(publisher)),
            IsSystem = sources.Any(source => source.IsSystem),
            IsUnsupported = sources.All(source => !source.CanToggle),
        };
    }

    private static int TierOf(SimpleMenuItem item) => item.IsUnsupported
        ? 3
        : item.IsSystem
            ? 0
            : item.Kind == EntryKind.ComHandler
                ? 1
                : 2;
}
