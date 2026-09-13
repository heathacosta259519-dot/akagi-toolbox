using ContextMenuEditor.Models;
using Microsoft.Win32;

namespace ContextMenuEditor.Services;

public sealed class RegistryScanner
{
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
        AddOrphanEntries(entries, blocked, handlerClsids);
        return entries;
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

                entries.Add(CreateStaticEntry(scope, location, verbName, verb));
            }
        }
    }

    private MenuEntry CreateStaticEntry(HiveScope scope, LocationDefinition location, string verbName, RegistryKey verb)
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

        return new MenuEntry
        {
            Id = $"static|{scope}|{location.SubPath}|{verbName}",
            DisplayName = EntryNameResolver.ForVerb(muiVerb, keyDefault, verbName),
            Kind = kind,
            Scope = scope,
            View = RegistryViewKind.X64,
            Location = location.Kind,
            RegistryPath = BuildRegistryPath(scope, RegistryViewKind.X64, $@"{location.SubPath}\shell\{verbName}"),
            KeyPath = $@"{location.SubPath}\shell\{verbName}",
            Command = command ?? delegateExecute ?? explorerCommand,
            Clsid = ClsidNormalizer.Normalize(explorerCommand),
            IconSource = iconPath,
            Publisher = _publishers.FromFile(iconPath) ?? _publishers.FromFile(executable),
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
                var clsid = ClsidNormalizer.Normalize(handler?.GetValue(null) as string);
                if (clsid != null)
                {
                    handlerClsids[view].Add(clsid);
                }

                var serverPath = clsid != null ? _publishers.ResolveServerPath(clsid) : null;

                entries.Add(new MenuEntry
                {
                    Id = $"handler|{scope}|{view}|{location.SubPath}|{handlerName}",
                    DisplayName = EntryNameResolver.Clean(handlerName),
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
                });
            }
        }
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
