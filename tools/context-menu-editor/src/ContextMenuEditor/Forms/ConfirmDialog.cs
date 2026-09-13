namespace ContextMenuEditor.Forms;

public sealed class ConfirmDialog : Form
{
    private ConfirmDialog(string title, string detail, string? warning, string confirmText, Image? icon)
    {
        Text = "确认操作";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(4);

        var iconBox = new PictureBox
        {
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = icon ?? SystemIcons.Application.ToBitmap(),
            Margin = new Padding(0, 2, 14, 0),
        };

        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            MaximumSize = new Size(430, 0),
            Margin = new Padding(0, 2, 0, 8),
        };

        var detailLabel = new Label
        {
            Text = detail,
            AutoSize = true,
            MaximumSize = new Size(430, 0),
            Margin = new Padding(0, 0, 0, 6),
        };

        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 14, 14, 6),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(iconBox, 0, 0);
        layout.SetRowSpan(iconBox, 3);
        layout.Controls.Add(titleLabel, 1, 0);
        layout.Controls.Add(detailLabel, 1, 1);

        if (!string.IsNullOrWhiteSpace(warning))
        {
            var warningLabel = new Label
            {
                Text = warning,
                AutoSize = true,
                ForeColor = Color.FromArgb(192, 0, 0),
                MaximumSize = new Size(430, 0),
                Margin = new Padding(0, 4, 0, 0),
            };
            layout.Controls.Add(warningLabel, 1, 2);
        }

        var cancelButton = new Button
        {
            Text = "取消",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(8, 8, 0, 0),
            Padding = new Padding(10, 3, 10, 3),
        };

        var confirmButton = new Button
        {
            Text = confirmText,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            DialogResult = DialogResult.OK,
            Margin = new Padding(8, 8, 0, 0),
            Padding = new Padding(10, 3, 10, 3),
        };

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 4, 14, 12),
        };
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(confirmButton);

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };
        root.Controls.Add(layout, 0, 0);
        root.Controls.Add(buttonPanel, 0, 1);

        Controls.Add(root);
        AcceptButton = confirmButton;
        CancelButton = cancelButton;
    }

    public static bool Show(IWin32Window owner, string title, string detail, string? warning, string confirmText, Image? icon)
    {
        using var dialog = new ConfirmDialog(title, detail, warning, confirmText, icon);
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }
}
