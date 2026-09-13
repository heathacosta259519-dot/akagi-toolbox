using System.Diagnostics;
using Microsoft.Win32;

namespace ContextMenuEditor.Services;

public sealed class PublisherResolver
{
    private readonly Dictionary<string, string?> _fileCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _serverPathCache = new(StringComparer.OrdinalIgnoreCase);

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

        var result = Resolve(path);
        _fileCache[path] = result;
        return result;
    }

    public string? ResolveServerPath(string clsid)
    {
        if (_serverPathCache.TryGetValue(clsid, out var cached))
        {
            return cached;
        }

        var result = FindServerPath(clsid);
        _serverPathCache[clsid] = result;
        return result;
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

    private static string? FindServerPath(string clsid)
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
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
}
