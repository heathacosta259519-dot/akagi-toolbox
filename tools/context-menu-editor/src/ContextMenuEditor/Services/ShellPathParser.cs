namespace ContextMenuEditor.Services;

public static class ShellPathParser
{
    public static string? ExtractExecutable(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        var value = Environment.ExpandEnvironmentVariables(commandLine.Trim());
        if (value.StartsWith('"'))
        {
            var end = value.IndexOf('"', 1);
            return end > 1 ? value[1..end] : null;
        }

        var cut = value.IndexOfAny([' ', '\t', ',']);
        var candidate = (cut < 0 ? value : value[..cut]).Trim();
        return candidate.Length > 0 ? candidate : null;
    }

    public static (string Path, int Index)? ParseIconValue(string? iconValue)
    {
        if (string.IsNullOrWhiteSpace(iconValue))
        {
            return null;
        }

        var value = Environment.ExpandEnvironmentVariables(iconValue.Trim());
        if (value.StartsWith('@'))
        {
            value = value[1..];
        }

        var index = 0;
        var comma = value.LastIndexOf(',');
        if (comma > 0)
        {
            var tail = value[(comma + 1)..].Trim();
            if (tail.Length > 0 && tail.All(c => char.IsDigit(c) || c == '-'))
            {
                int.TryParse(tail, out index);
                value = value[..comma];
            }
        }

        value = value.Trim().Trim('"');
        return value.Length > 0 ? (value, index) : null;
    }
}
