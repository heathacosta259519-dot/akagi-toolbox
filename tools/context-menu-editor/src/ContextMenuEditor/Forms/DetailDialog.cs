using ContextMenuEditor.Models;
using ContextMenuEditor.Services;

namespace ContextMenuEditor.Forms;

public sealed class DetailDialog : Form
{
    public bool ToggleRequested { get; private set; }

    private DetailDialog(MenuEntry entry, Image? icon)
    {
        Text = "菜单项详情";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(4);

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };

        root.Controls.Add(BuildHeader(entry, icon), 0, 0);
        root.Controls.Add(BuildDetails(entry), 0, 1);
        root.Controls.Add(BuildButtons(entry), 0, 2);

        Controls.Add(root);
    }

    public static bool Show(IWin32Window owner, MenuEntry entry, Image? icon)
    {
        using var dialog = new DetailDialog(entry, icon);
        return dialog.ShowDialog(owner) == DialogResult.OK && dialog.ToggleRequested;
    }

    private Control BuildHeader(MenuEntry entry, Image? icon)
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 14, 14, 4),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var iconBox = new PictureBox
        {
            Size = new Size(32, 32),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = icon ?? SystemIcons.Application.ToBitmap(),
            Margin = new Padding(0, 4, 12, 0),
        };

        var title = new Label
        {
            Text = entry.DisplayName,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            MaximumSize = new Size(430, 0),
            Margin = new Padding(0, 8, 0, 0),
        };

        panel.Controls.Add(iconBox, 0, 0);
        panel.Controls.Add(title, 1, 0);
        return panel;
    }

    private Control BuildDetails(MenuEntry entry)
    {
        var panel = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 4, 14, 4),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var row = 0;
        AddRow(panel, ref row, "状态", entry.StateText + (entry.StateDetail is null ? string.Empty : $"（{entry.StateDetail}）"));
        AddRow(panel, ref row, "类型", entry.KindText + (entry.IsSystem ? "（系统关键项）" : string.Empty));
        AddRow(panel, ref row, "位置", RegistryLocations.DisplayNameFor(entry.Location));
        AddRow(panel, ref row, "作用域", entry.ScopeText);
        AddRow(panel, ref row, "来源", entry.Publisher ?? "—");
        AddTextRow(panel, ref row, "命令 / CLSID", entry.Command);
        AddTextRow(panel, ref row, "注册表路径", entry.RegistryPath);

        if (!entry.CanToggle && !entry.IsOrphan)
        {
            AddRow(panel, ref row, "说明", entry.Kind == EntryKind.ExplorerCommand
                ? "新型命令（ExplorerCommand），当前版本暂不支持禁用"
                : "缺少可识别的标识，当前无法操作");
        }

        return panel;
    }

    private Control BuildButtons(MenuEntry entry)
    {
        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 6, 14, 12),
        };

        panel.Controls.Add(CreateButton("关闭", (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }));

        if (entry.CanToggle || entry.IsOrphan)
        {
            var text = entry.IsOrphan
                ? "清理残留记录"
                : entry.State == EntryState.Enabled ? "禁用此菜单项" : "恢复显示";
            panel.Controls.Add(CreateButton(text, (_, _) =>
            {
                ToggleRequested = true;
                DialogResult = DialogResult.OK;
                Close();
            }));
        }

        panel.Controls.Add(CreateButton("在注册表中打开", (_, _) => RegistryNavigator.Open(entry.RegistryPath)));

        panel.Controls.Add(CreateButton("复制路径", (_, _) =>
        {
            try
            {
                Clipboard.SetText(entry.RegistryPath);
            }
            catch
            {
            }
        }));

        return panel;
    }

    private Button CreateButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(8, 0, 0, 0),
            Padding = new Padding(10, 3, 10, 3),
        };
        button.Click += onClick;
        return button;
    }

    private void AddRow(TableLayoutPanel panel, ref int row, string label, string value)
    {
        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            ForeColor = Color.FromArgb(90, 90, 90),
            Margin = new Padding(0, 3, 14, 3),
        }, 0, row);

        panel.Controls.Add(new Label
        {
            Text = value,
            AutoSize = true,
            MaximumSize = new Size(430, 0),
            Margin = new Padding(0, 3, 0, 3),
        }, 1, row);

        row++;
    }

    private void AddTextRow(TableLayoutPanel panel, ref int row, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            ForeColor = Color.FromArgb(90, 90, 90),
            Margin = new Padding(0, 3, 14, 3),
        }, 0, row);

        panel.Controls.Add(new TextBox
        {
            Text = value,
            ReadOnly = true,
            Width = 430,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 3, 0, 3),
        }, 1, row);

        row++;
    }
}
