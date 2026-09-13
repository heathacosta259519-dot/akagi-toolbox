using ContextMenuEditor.Models;

namespace ContextMenuEditor.Services;

public static class SimpleMenuBuilder
{
    public static List<SimpleMenuItem> Build(IEnumerable<MenuEntry> entries, MenuScene scene)
    {
        var locations = scene.Locations().ToHashSet();
        var relevant = entries
            .Where(entry => !entry.IsOrphan && !entry.IsInactive && locations.Contains(entry.Location))
            .ToArray();

        var items = GroupEntries(relevant).Select(CreateItem).ToList();

        var byEntryId = new Dictionary<string, SimpleMenuItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            foreach (var source in item.Sources)
            {
                byEntryId.TryAdd(source.Id, item);
            }
        }

        foreach (var item in items)
        {
            item.IsAncestorHidden = IsAncestorHidden(item, byEntryId);
        }

        return Flatten(items, byEntryId);
    }

    private static List<List<MenuEntry>> GroupEntries(MenuEntry[] entries)
    {
        var groups = new List<List<MenuEntry>>();
        var byKey = new Dictionary<string, List<MenuEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var key = GroupKeyFor(entry);
            if (key == null)
            {
                groups.Add([entry]);
                continue;
            }

            if (!byKey.TryGetValue(key, out var group))
            {
                group = [];
                byKey[key] = group;
            }

            group.Add(entry);
        }

        groups.AddRange(byKey.Values);
        return groups;
    }

    private static string? GroupKeyFor(MenuEntry entry) => entry.Kind switch
    {
        EntryKind.ComHandler when entry.Clsid != null => "com|" + entry.Clsid,
        EntryKind.ComHandler => "handler|" + entry.KeyPath,
        _ => "verb|" + entry.KeyPath,
    };

    private static SimpleMenuItem CreateItem(List<MenuEntry> sources)
    {
        var primary = sources[0];
        var publisher = sources
            .Select(source => source.Publisher)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return new SimpleMenuItem
        {
            Id = GroupKeyFor(primary) ?? primary.Id,
            DisplayName = primary.DisplayName,
            Kind = primary.Kind,
            Sources = sources,
            Publisher = publisher,
            Owner = publisher ?? (primary.Kind == EntryKind.ComHandler ? "其他扩展" : "其他菜单项"),
            ParentId = primary.ParentId,
            Indent = primary.Indent,
            HasChildren = sources.Any(source => source.HasChildren),
            IsSystem = sources.Any(source => source.IsSystem),
            IsUnsupported = sources.All(source => !source.CanToggle),
        };
    }

    private static bool IsAncestorHidden(SimpleMenuItem item, Dictionary<string, SimpleMenuItem> byEntryId)
    {
        var current = item;
        for (var depth = 0; depth < 16 && current.ParentId != null; depth++)
        {
            if (!byEntryId.TryGetValue(current.ParentId, out var parent))
            {
                return false;
            }

            if (!parent.IsShown)
            {
                return true;
            }

            current = parent;
        }

        return false;
    }

    private static List<SimpleMenuItem> Flatten(List<SimpleMenuItem> items, Dictionary<string, SimpleMenuItem> byEntryId)
    {
        var roots = new List<SimpleMenuItem>();
        var children = new Dictionary<SimpleMenuItem, List<SimpleMenuItem>>();

        foreach (var item in items)
        {
            var parent = item.ParentId != null && byEntryId.TryGetValue(item.ParentId, out var found) ? found : null;
            if (parent == null)
            {
                roots.Add(item);
                continue;
            }

            if (!children.TryGetValue(parent, out var siblings))
            {
                siblings = [];
                children[parent] = siblings;
            }

            siblings.Add(item);
        }

        var ordered = new List<SimpleMenuItem>();

        void Emit(SimpleMenuItem item)
        {
            ordered.Add(item);
            if (!children.TryGetValue(item, out var kids))
            {
                return;
            }

            foreach (var kid in Sort(kids))
            {
                Emit(kid);
            }
        }

        foreach (var root in Sort(roots))
        {
            Emit(root);
        }

        return ordered;
    }

    private static IEnumerable<SimpleMenuItem> Sort(IEnumerable<SimpleMenuItem> items) =>
        items.OrderBy(TierOf).ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase);

    private static int TierOf(SimpleMenuItem item) => item.IsUnsupported
        ? 3
        : item.IsSystem
            ? 0
            : item.Kind == EntryKind.ComHandler
                ? 2
                : 1;
}
