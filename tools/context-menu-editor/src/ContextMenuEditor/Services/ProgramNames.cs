namespace ContextMenuEditor.Services;

public static class ProgramNames
{
    private static readonly string[] ContainerFolders =
    [
        "Common",
        "Common Files",
        "Windows Kits",
        "WindowsApps",
        "ModifiableWindowsApps",
        "Internet Explorer",
        "WindowsPowerShell",
        "dotnet",
    ];

    public static string? FromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var full = path.Trim().Trim('"');

        foreach (var root in ProductRoots())
        {
            var relative = RelativeTo(full, root);
            if (relative == null)
            {
                continue;
            }

            var segments = relative.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                if (ContainerFolders.Contains(segment, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                return segment;
            }

            return null;
        }

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return !string.IsNullOrEmpty(windows) && IsUnder(full, windows) ? "Windows" : null;
    }

    private static IEnumerable<string> ProductRoots()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles))
        {
            yield return programFiles;
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(programFilesX86))
        {
            yield return programFilesX86;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            yield return Path.Combine(localAppData, "Programs");
        }

        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(roaming))
        {
            yield return roaming;
        }

        if (!string.IsNullOrEmpty(localAppData))
        {
            yield return localAppData;
        }
    }

    private static string? RelativeTo(string path, string root)
    {
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        var prefix = root.TrimEnd('\\') + "\\";
        return IsUnder(path, root) ? path[prefix.Length..] : null;
    }

    private static bool IsUnder(string path, string root) =>
        path.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
}
