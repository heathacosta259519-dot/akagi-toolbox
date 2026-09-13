namespace ContextMenuEditor.Services;

public static class ClsidNormalizer
{
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (!value.StartsWith('{'))
        {
            value = "{" + value + "}";
        }

        return Guid.TryParse(value, out var guid)
            ? guid.ToString("B").ToUpperInvariant()
            : null;
    }
}
