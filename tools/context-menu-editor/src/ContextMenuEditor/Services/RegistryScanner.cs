using ContextMenuEditor.Models;
using Microsoft.Win32;

namespace ContextMenuEditor.Services;

public sealed class RegistryScanner
{
    private const int MaxCascadeDepth = 3;

    private readonly PublisherResolver _publishers;

    public RegistryScanner(PublisherResolver publishers)
    {
        _publishers = publishers;
    }

    public List<MenuEntry> ScanAll()
    {
        var entries = new List<MenuEntry>();
        var blocked = LoadBlockedLists();
        var handlerClsids = new Dictionary<RegistryViewKind, HashSet<string>>
        {
            [RegistryViewKind.X64] = new(StringComparer.OrdinalIgnoreCase),
            [RegistryViewKind.X86] = new(StringComparer.OrdinalIgnoreCase),
        };

        foreach (var scope in new[] { HiveScope.Machine, HiveScope.CurrentUser })
        {
            ScanStatics(scope, entries);
            ScanHandlers(scope, RegistryViewKind.X64, entries, handlerClsids);
            ScanHandlers(scope, RegistryViewKind.X86, entries, handlerClsids);
        }

        ApplyBlockedStates(entries, blocked);
        ApplyParentFlags(entries);
        AddOrphanEntries(entries, blocked, handlerClsids);
        return entries;
    }

    private static void ApplyParentFlags(List<MenuEntry> entries)
    {
        var parents = entries
            .Where(entry => entry.ParentId != null)
            .Select(entry => entry.ParentId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries.Where(entry => parents.Contains(entry.Id)))
        {
            entry.HasChildren = true;
        }
    }

    private void ScanStatics(HiveScope scope, List<MenuEntry> entries)
    {
        using var classes = OpenClasses(scope, RegistryViewKind.X64);
        if (classes == null)
        {
            return;
        }

        foreach (var location in RegistryLocations.All.Where(item => item.HasShell))
        {
            using var shell = classes.OpenSubKey($@"{location.SubPath}\shell");
            if (shell == null)
            {
                continue;
            }

            foreach (var verbName in shell.GetSubKeyNames())
            {
                using var verb = shell.OpenSubKey(verbName);
                if (verb == null)
                {
                    continue;
                }

                var keyPath = $@"{location.SubPath}\shell\{verbName}";
                var entry = CreateStaticEntry(scope, location, verbName, verb, keyPath, parentId: null, indent: 0);
                entries.Add(entry);
                ScanCascade(scope, location, verb, entry, keyPath, entries, depth: 1);
            }
        }
    }

    private void ScanCascade(
        HiveScope scope,
        LocationDefinition location,
        RegistryKey verb,
        MenuEntry parent,
        string parentKeyPath,
        List<MenuEntry> entries,
        int depth)
    {
        if (depth > MaxCascadeDepth)
        {
            return;
        }

        foreach (var (childScope, containerPath) in CascadeContainers(scope, verb, parentKeyPath))
        {
            using var baseKey = RegistryKey.OpenBaseKey(ToHive(childScope), RegistryView.Registry64);
            using var container = OpenCascadeContainer(baseKey, $@"Software\Classes\{containerPath}");
            if (container == null)
            {
                continue;
            }

            foreach (var childName in container.GetSubKeyNames())
            {
                using var child = container.OpenSubKey(childName);
                if (child == null)
                {
                    continue;
                }

                var childPath = $@"{containerPath}\{childName}";
                var childEntry = CreateStaticEntry(childScope, location, childName, child, childPath, parent.Id, depth);
                entries.Add(childEntry);
                ScanCascade(childScope, location, child, childEntry, childPath, entries, depth + 1);
            }
        }
    }

    private static List<(HiveScope Scope, string KeyPath)> CascadeContainers(HiveScope scope, RegistryKey verb, string parentKeyPath)
    {
        var containers = new List<(HiveScope, string)>();

        if (!string.IsNullOrWhiteSpace(verb.GetValue("SubCommands") as string))
        {
            containers.Add((scope, $@"{parentKeyPath}\shell"));
        }

        var extended = verb.GetValue("ExtendedSubCommandsKey") as string;
        if (!string.IsNullOrWhiteSpace(extended))
        {
            var resolved = ResolveExtendedContainer(scope, extended);
            if (resolved != null)
            {
                containers.Add(resolved.Value);
            }
        }

        return containers;
    }

    private static (HiveScope Scope, string KeyPath)? ResolveExtendedContainer(HiveScope scope, string value)
    {
        const string classesPrefix = @"Software\Classes\";
        var trimmed = value.Trim().TrimStart('\\');
        var relative = trimmed.StartsWith(classesPrefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed[classesPrefix.Length..]
            : trimmed;

        var scopes = scope == HiveScope.CurrentUser
            ? new[] { HiveScope.CurrentUser, HiveScope.Machine }
            : new[] { HiveScope.Machine, HiveScope.CurrentUser };

        foreach (var candidate in new[] { $@"{relative}\shell", relative })
        {
            foreach (var candidateScope in scopes)
            {
                using var baseKey = RegistryKey.OpenBaseKey(ToHive(candidateScope), RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(classesPrefix + candidate);
                if (key != null && key.GetSubKeyNames().Length > 0)
                {
                    return (candidateScope, candidate);
                }
            }
        }

        return null;
    }

    private static RegistryKey? OpenCascadeContainer(RegistryKey baseKey, string keyPath)
    {
        using var direct = baseKey.OpenSubKey(keyPath);
        if (direct != null)
        {
            return baseKey.OpenSubKey(keyPath);
        }

        var withoutShell = keyPath.EndsWith(@"\shell", StringComparison.OrdinalIgnoreCase)
            ? keyPath[..^@"\shell".Length]
            : keyPath;
        return baseKey.OpenSubKey(withoutShell);
    }

    private MenuEntry CreateStaticEntry(
        HiveScope scope,
        LocationDefinition location,
        string verbName,
        RegistryKey verb,
        string keyPath,
        string? parentId,
        int indent)
    {
        var muiVerb = verb.GetValue("MUIVerb") as string;
        var keyDefault = verb.GetValue(null) as string;
        var iconValue = verb.GetValue("Icon") as string;
        var explorerCommand = verb.GetValue("ExplorerCommandHandler") as string;
        var delegateExecute = verb.GetValue("DelegateExecute") as string;
        using var commandKey = verb.OpenSubKey("command");
        var command = commandKey?.GetValue(null) as string;
        var disabled = verb.GetValue("LegacyDisable") != null;

        var executable = ShellPathParser.ExtractExecutable(command);
        var iconPath = ShellPathParser.ParseIconValue(iconValue)?.Path ?? executable;
        var kind = explorerCommand != null ? EntryKind.ExplorerCommand : EntryKind.StaticVerb;
        var clsid = ClsidNormalizer.Normalize(explorerCommand);
        var serverPath = clsid != null ? _publishers.ResolveServerPath(clsid) : null;
        var resolvedIcon = iconPath ?? serverPath;

        return new MenuEntry
        {
            Id = parentId == null ? $"static|{scope}|{location.SubPath}|{verbName}" : $"static|{scope}|{keyPath}",
            DisplayName = EntryNameResolver.ForVerb(muiVerb, keyDefault, verbName),
            Kind = kind,
            Scope = scope,
            View = RegistryViewKind.X64,
            Location = location.Kind,
            RegistryPath = BuildRegistryPath(scope, RegistryViewKind.X64, keyPath),
            KeyPath = keyPath,
            ParentId = parentId,
            Indent = indent,
            Command = command ?? delegateExecute ?? explorerCommand,
            Clsid = clsid,
            IconSource = resolvedIcon,
            Publisher = _publishers.FromFile(iconPath) ?? _publishers.FromFile(executable) ?? _publishers.FromFile(serverPath),
            IsSystem = RegistryLocations.IsSystemItem(location.Kind, verbName),
            State = disabled ? EntryState.Disabled : EntryState.Enabled,
            StateDetail = disabled ? "LegacyDisable" : null,
        };
    }

    private void ScanHandlers(HiveScope scope, RegistryViewKind view, List<MenuEntry> entries, Dictionary<RegistryViewKind, HashSet<string>> handlerClsids)
    {
        using var classes = OpenClasses(scope, view);
        if (classes == null)
        {
            return;
        }

        foreach (var location in RegistryLocations.All.Where(item => item.HasShellex))
        {
            using var root = classes.OpenSubKey($@"{location.SubPath}\shellex\ContextMenuHandlers");
            if (root == null)
            {
                continue;
            }

            foreach (var handlerName in root.GetSubKeyNames())
            {
                using var handler = root.OpenSubKey(handlerName);
                var handlerValue = handler?.GetValue(null) as string;
                var clsid = ClsidNormalizer.Normalize(handlerValue) ?? ClsidNormalizer.Normalize(handlerName);
                if (clsid != null)
                {
                    handlerClsids[view].Add(clsid);
                }

                var serverPath = clsid != null ? _publishers.ResolveServerPath(clsid, RegistryViewKind.X64) : null;
                var isInactive = clsid == null || serverPath == null;

                entries.Add(new MenuEntry
                {
                    Id = $"handler|{scope}|{view}|{location.SubPath}|{handlerName}",
                    DisplayName = EntryNameResolver.ForHandler(
                        handlerName,
                        ResolveClsidName(clsid),
                        _publishers.DescriptionFromFile(serverPath),
                        handlerValue),
                    Kind = EntryKind.ComHandler,
                    Scope = scope,
                    View = view,
                    Location = location.Kind,
                    RegistryPath = BuildRegistryPath(scope, view, $@"{location.SubPath}\shellex\ContextMenuHandlers\{handlerName}"),
                    KeyPath = $@"{location.SubPath}\shellex\ContextMenuHandlers\{handlerName}",
                    Command = clsid,
                    Clsid = clsid,
                    IconSource = serverPath,
                    Publisher = _publishers.FromFile(serverPath),
                    IsInactive = isInactive,
                });
            }
        }
    }

    private static string? ResolveClsidName(string? clsid)
    {
        if (clsid == null)
        {
            return null;
        }

        foreach (var scope in new[] { HiveScope.Machine, HiveScope.CurrentUser })
        {
            foreach (var view in new[] { RegistryViewKind.X64, RegistryViewKind.X86 })
            {
                using var key = RegistryKey
                    .OpenBaseKey(ToHive(scope), ToView(view))
                    .OpenSubKey($@"SOFTWARE\Classes\CLSID\{clsid}");
                var name = (key?.GetValue(null) as string)?.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
        }

        return null;
    }

    private static void ApplyBlockedStates(List<MenuEntry> entries, Dictionary<(HiveScope Scope, RegistryViewKind View), HashSet<string>> blocked)
    {
        foreach (var entry in entries.Where(item => item.Kind == EntryKind.ComHandler && item.Clsid != null))
        {
            if (blocked[(HiveScope.Machine, entry.View)].Contains(entry.Clsid!))
            {
                entry.State = EntryState.Disabled;
                entry.StateDetail = "已屏蔽（本机）";
            }
            else if (blocked[(HiveScope.CurrentUser, entry.View)].Contains(entry.Clsid!))
            {
                entry.State = EntryState.Disabled;
                entry.StateDetail = "已屏蔽（当前用户）";
            }
        }
    }

    private static void AddOrphanEntries(
        List<MenuEntry> entries,
        Dictionary<(HiveScope Scope, RegistryViewKind View), HashSet<string>> blocked,
        Dictionary<RegistryViewKind, HashSet<string>> handlerClsids)
    {
        foreach (var view in new[] { RegistryViewKind.X64, RegistryViewKind.X86 })
        {
            foreach (var scope in new[] { HiveScope.Machine, HiveScope.CurrentUser })
            {
                foreach (var clsid in blocked[(scope, view)])
                {
                    if (handlerClsids[view].Contains(clsid))
                    {
                        continue;
                    }

                    entries.Add(new MenuEntry
                    {
                        Id = $"orphan|{scope}|{view}|{clsid}",
                        DisplayName = clsid,
                        Kind = EntryKind.ComHandler,
                        Scope = scope,
                        View = view,
                        Location = LocationKind.BlockedOrphan,
                        RegistryPath = BuildBlockedPath(scope, view),
                        KeyPath = string.Empty,
                        Clsid = clsid,
                        State = EntryState.Orphan,
                        StateDetail = "屏蔽列表中未找到对应注册",
                    });
                }
            }
        }
    }

    private static Dictionary<(HiveScope Scope, RegistryViewKind View), HashSet<string>> LoadBlockedLists()
    {
        var result = new Dictionary<(HiveScope, RegistryViewKind), HashSet<string>>();
        foreach (var scope in new[] { HiveScope.Machine, HiveScope.CurrentUser })
        {
            foreach (var view in new[] { RegistryViewKind.X64, RegistryViewKind.X86 })
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(ToHive(scope), ToView(view));
                    using var blockedKey = baseKey.OpenSubKey(RegistryPaths.Blocked);
                    if (blockedKey != null)
                    {
                        foreach (var name in blockedKey.GetValueNames())
                        {
                            var clsid = ClsidNormalizer.Normalize(name);
                            if (clsid != null)
                            {
                                set.Add(clsid);
                            }
                        }
                    }
                }
                catch
                {
                }

                result[(scope, view)] = set;
            }
        }

        return result;
    }

    private static RegistryKey? OpenClasses(HiveScope scope, RegistryViewKind view)
    {
        try
        {
            return RegistryKey.OpenBaseKey(ToHive(scope), ToView(view)).OpenSubKey("SOFTWARE\\Classes");
        }
        catch
        {
            return null;
        }
    }

    internal static RegistryHive ToHive(HiveScope scope) =>
        scope == HiveScope.Machine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;

    internal static RegistryView ToView(RegistryViewKind view) =>
        view == RegistryViewKind.X64 ? RegistryView.Registry64 : RegistryView.Registry32;

    internal static string BuildRegistryPath(HiveScope scope, RegistryViewKind view, string subPath)
    {
        var hive = scope == HiveScope.Machine ? "HKEY_LOCAL_MACHINE" : "HKEY_CURRENT_USER";
        var prefix = view == RegistryViewKind.X86 ? @"SOFTWARE\Classes\WOW6432Node" : @"SOFTWARE\Classes";
        return $@"{hive}\{prefix}\{subPath}";
    }

    internal static string BuildBlockedPath(HiveScope scope, RegistryViewKind view)
    {
        var hive = scope == HiveScope.Machine ? "HKEY_LOCAL_MACHINE" : "HKEY_CURRENT_USER";
        return $@"{hive}\{RegistryPaths.Blocked}";
    }
}
