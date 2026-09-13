using ContextMenuEditor.Models;
using ContextMenuEditor.Services;

namespace ContextMenuEditor.Forms;

public sealed class HistoryDialog : Form
{
    private readonly JournalService _journal;
    private readonly ToggleService _toggles = new();
    private readonly ListView _list = new();

    public bool Changed { get; private set; }

    private HistoryDialog(JournalService journal)
    {
        _journal = journal;
        Text = "操作历史";
        Width = 940;
        Height = 540;
        MinimumSize = new Size(720, 420);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;

        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.HideSelection = false;
        _list.Columns.Add("时间", 150);
        _list.Columns.Add("操作", 100);
        _list.Columns.Add("目标", 240);
        _list.Columns.Add("类型", 100);
        _list.Columns.Add("作用域", 100);
        _list.Columns.Add("详情", 240);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 8, 10, 8),
        };
        buttons.Controls.Add(CreateButton("撤销选中", (_, _) => UndoSelected()));
        buttons.Controls.Add(CreateButton("刷新", (_, _) => Reload()));
        buttons.Controls.Add(CreateButton("关闭", (_, _) => Close()));

        Controls.Add(_list);
        Controls.Add(buttons);

        Reload();
    }

    public static bool Show(IWin32Window owner, JournalService journal)
    {
        using var dialog = new HistoryDialog(journal);
        dialog.ShowDialog(owner);
        return dialog.Changed;
    }

    private Button CreateButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(10, 3, 10, 3),
        };
        button.Click += onClick;
        return button;
    }

    private void Reload()
    {
        var records = _journal.ReadAll().Reverse().ToArray();

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var record in records)
        {
            var item = new ListViewItem(record.Time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"))
            {
                Tag = record,
            };
            item.SubItems.Add(record.ActionText);
            item.SubItems.Add(record.Target);
            item.SubItems.Add(KindText(record.Kind));
            item.SubItems.Add(ScopeText(record));
            item.SubItems.Add(DetailText(record));
            _list.Items.Add(item);
        }

        _list.EndUpdate();
    }

    private void UndoSelected()
    {
        if (_list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not JournalRecord record)
        {
            MessageBox.Show(this, "请先选择一条记录。", "操作历史", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            if (record.Kind == "classic-menu")
            {
                var reverse = record.Action == "classic-on" ? "classic-off" : "classic-on";
                if (record.Action == "classic-on")
                {
                    ClassicMenuService.Disable();
                }
                else
                {
                    ClassicMenuService.Enable();
                }

                _journal.Append(JournalRecord.ForClassicMenu(reverse));
            }
            else
            {
                var target = record.ToTarget();
                switch (record.Action)
                {
                    case "disable":
                        _toggles.Enable(target);
                        _journal.Append(JournalRecord.FromToggle(target, "enable"));
                        break;
                    case "enable":
                    case "clean":
                        _toggles.Disable(target);
                        _journal.Append(JournalRecord.FromToggle(target, "disable"));
                        break;
                    default:
                        throw new InvalidOperationException("无法撤销该操作。");
                }
            }

            Changed = true;
            Reload();
        }
        catch (Exception exception)
        {
            var message = exception is UnauthorizedAccessException or System.Security.SecurityException
                ? "没有权限修改注册表，请以管理员身份运行。"
                : exception.Message;
            MessageBox.Show(this, "撤销失败：" + message, "右键菜单编辑器", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string KindText(string kind) => kind switch
    {
        "StaticVerb" => "静态项",
        "ComHandler" => "COM 扩展",
        "classic-menu" => "经典菜单",
        _ => kind,
    };

    private static string ScopeText(JournalRecord record)
    {
        var scope = record.Scope switch
        {
            "Machine" => "本机",
            "CurrentUser" => "当前用户",
            _ => record.Scope,
        };

        return record.View == "X86" ? scope + " (32位)" : scope;
    }

    private static string DetailText(JournalRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.KeyPath))
        {
            return record.KeyPath;
        }

        return record.Clsid ?? string.Empty;
    }
}
