using System.Diagnostics;
using ContextMenuEditor.Models;
using Microsoft.Win32;

namespace ContextMenuEditor.Services;

public sealed class PublisherResolver
{
    private readonly Dictionary<string, string?> _fileCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _serverPathCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _descriptionCache = new(StringComparer.OrdinalIgnoreCase);

    public string? FromFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (_fileCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var result = ProgramNames.FromPath(path) ?? Resolve(path);
        _fileCache[path] = result;
        return result;
    }

    public string? ResolveServerPath(string clsid, RegistryViewKind? view = null)
    {
        var key = view == null ? clsid : clsid + "|" + view;
        if (_serverPathCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var result = FindServerPath(clsid, view);
        _serverPathCache[key] = result;
        return result;
    }

    public string? DescriptionFromFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (_descriptionCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var result = ResolveDescription(path);
        _descriptionCache[path] = result;
        return result;
    }

    private static string? ResolveDescription(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var description = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
            return string.IsNullOrWhiteSpace(description) ? null : description;
        }
        catch
        {
            return null;
        }
    }

    private static string? Resolve(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var info = FileVersionInfo.GetVersionInfo(path);
            foreach (var candidate in new[] { info.ProductName, info.FileDescription, info.CompanyName })
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate.Trim();
                }
            }

            return Path.GetFileNameWithoutExtension(path);
        }
        catch
        {
            return null;
        }
    }

    private static string? FindServerPath(string clsid, RegistryViewKind? view)
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var candidate in ViewsFor(view))
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, candidate);
                    using var key = baseKey.OpenSubKey($@"SOFTWARE\Classes\CLSID\{clsid}\InprocServer32");
                    if (key?.GetValue(null) is string path && path.Trim().Length > 0)
                    {
                        return Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
                    }
                }
                catch
                {
                }
            }
        }

        return null;
    }

    private static IEnumerable<RegistryView> ViewsFor(RegistryViewKind? view) => view switch
    {
        RegistryViewKind.X64 => [RegistryView.Registry64],
        RegistryViewKind.X86 => [RegistryView.Registry32],
        _ => [RegistryView.Registry64, RegistryView.Registry32],
    };
}
