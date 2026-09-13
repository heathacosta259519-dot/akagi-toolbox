namespace ContextMenuEditor.Services;

public static class RegistryPaths
{
    public const string Blocked = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";

    public const string ClassicMenuClsid = "{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";

    public const string ClassicMenuKey =
        @"SOFTWARE\Classes\CLSID\" + ClassicMenuClsid + @"\InprocServer32";
}
