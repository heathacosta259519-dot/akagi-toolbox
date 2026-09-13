namespace ContextMenuEditor.Services;

public static class EntryNameResolver
{
    public static string ForVerb(string? muiVerb, string? keyDefaultValue, string verbKeyName)
    {
        foreach (var candidate in new[] { muiVerb, keyDefaultValue })
        {
            var text = Clean(candidate);
            if (text.Length > 0)
            {
                return text;
            }
        }

        return Clean(verbKeyName);
    }

    public static string Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var value = raw.Trim();
        var resolved = NativeMethods.TryResolveIndirectString(value);
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            value = resolved;
        }

        const string marker = "\u0001";
        value = value.Replace("&&", marker, StringComparison.Ordinal);
        value = value.Replace("&", string.Empty, StringComparison.Ordinal);
        value = value.Replace(marker, "&", StringComparison.Ordinal);
        return value.Trim();
    }
}
