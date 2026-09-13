using ContextMenuEditor.Models;
using Microsoft.Win32;

namespace ContextMenuEditor.Services;

public sealed class ToggleService
{
    public void Disable(ToggleTarget target)
    {
        switch (target.Kind)
        {
            case EntryKind.StaticVerb:
                SetLegacyDisable(target, true);
                break;
            case EntryKind.ComHandler when target.Clsid != null:
                SetBlocked(target, true);
                break;
            case EntryKind.ExplorerCommand:
                throw new NotSupportedException("该菜单项为新型命令（ExplorerCommand），当前版本暂不支持禁用。");
            default:
                throw new NotSupportedException("该菜单项缺少可识别的标识，无法操作。");
        }
    }

    public void Enable(ToggleTarget target)
    {
        switch (target.Kind)
        {
            case EntryKind.StaticVerb:
                SetLegacyDisable(target, false);
                break;
            case EntryKind.ComHandler when target.Clsid != null:
                RemoveFromBlockedLists(target);
                break;
            default:
                throw new NotSupportedException("该菜单项缺少可识别的标识，无法操作。");
        }
    }

    public void CleanOrphan(ToggleTarget target) => RemoveFromBlockedLists(target);

    private static void SetLegacyDisable(ToggleTarget target, bool disable)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryScanner.ToHive(target.Scope), RegistryScanner.ToView(target.View));
        using var key = baseKey.OpenSubKey($@"SOFTWARE\Classes\{target.KeyPath}", writable: true)
            ?? throw new InvalidOperationException("未找到对应的注册表项，可能已被其他程序删除。");

        if (disable)
        {
            key.SetValue("LegacyDisable", string.Empty, RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue("LegacyDisable", false);
        }
    }

    private static void SetBlocked(ToggleTarget target, bool block)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryScanner.ToHive(target.Scope), RegistryScanner.ToView(target.View));
        using var key = baseKey.CreateSubKey(RegistryPaths.Blocked, true)
            ?? throw new InvalidOperationException("无法写入屏蔽列表。");

        if (block)
        {
            key.SetValue(target.Clsid!, string.Empty, RegistryValueKind.String);
            return;
        }

        key.DeleteValue(target.Clsid!, false);
    }

    private static void RemoveFromBlockedLists(ToggleTarget target)
    {
        if (target.Clsid == null)
        {
            return;
        }

        foreach (var scope in new[] { HiveScope.Machine, HiveScope.CurrentUser })
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryScanner.ToHive(scope), RegistryScanner.ToView(target.View));
            using var existing = baseKey.OpenSubKey(RegistryPaths.Blocked);
            if (existing == null || !ContainsValue(existing, target.Clsid))
            {
                continue;
            }

            try
            {
                using var writable = baseKey.OpenSubKey(RegistryPaths.Blocked, writable: true);
                writable?.DeleteValue(target.Clsid, false);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException)
            {
                throw new InvalidOperationException("没有权限修改系统屏蔽列表，请以管理员身份运行。", exception);
            }
        }
    }

    private static bool ContainsValue(RegistryKey key, string name) =>
        key.GetValueNames().Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase));
}
