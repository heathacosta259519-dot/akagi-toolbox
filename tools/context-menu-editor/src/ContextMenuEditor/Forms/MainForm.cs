using ContextMenuEditor.Models;
using ContextMenuEditor.Services;

namespace ContextMenuEditor.Forms;

public sealed class MainForm : Form
{
    private readonly PublisherResolver _publishers = new();
    private readonly RegistryScanner _scanner;
    private readonly ToggleService _toggles = new();
    private readonly JournalService _journal = new();
    private readonly SettingsService _settings = new();
    private readonly IconService _icons = new();
    private readonly ImageList _imageList = new();
    private readonly List<MenuEntry> _entries = [];
    private readonly ComboBox _modeBox = new();
    private readonly ComboBox _locationBox = new();
    private readonly ComboBox _sceneBox = new();
    private readonly Label _locationLabel = new();
    private readonly Label _sceneLabel = new();
    private readonly TextBox _searchBox = new();
    private readonly CheckBox _onlyDisabled = new();
    private readonly CheckBox _classicMenuBox = new();
    private bool _simpleMode;
    private MenuScene _scene = MenuScene.Files;
    private bool _suppressClassic;
    private readonly ListView _list = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private bool _suppressCheck;

    public MainForm()
    {
        _scanner = new RegistryScanner(_publishers);
        _simpleMode = _settings.SimpleMode;
        _scene = _settings.Scene;
        BuildLayout();
        Load += (_, _) =>
        {
            RefreshEntries();
            RefreshClassicMenuState();
        };
        FormClosed += (_, _) =>
        {
            _icons.Dispose();
            _imageList.Dispose();
        };
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        var area = Screen.FromControl(this).WorkingArea;
        ClientSize = new Size(
            Math.Min(1320, Math.Max(900, area.Width - 100)),
            Math.Min(820, Math.Max(520, area.Height - 100)));
    }

    private void BuildLayout()
    {
        Text = "右键菜单编辑器";
        MinimumSize = new Size(940, 560);
        StartPosition = FormStartPosition.CenterScreen;

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Padding = new Padding(10, 10, 10, 6),
        };

        top.Controls.Add(new Label { Text = "模式：", AutoSize = true, Margin = new Padding(0, 7, 2, 0) });
        _modeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _modeBox.Width = 130;
        _modeBox.Margin = new Padding(0, 3, 16, 3);
        _modeBox.Items.AddRange(["简单模式", "高级模式"]);
        _modeBox.SelectedIndex = _simpleMode ? 0 : 1;
        top.Controls.Add(_modeBox);

        _locationLabel.Text = "位置：";
        _locationLabel.AutoSize = true;
        _locationLabel.Margin = new Padding(0, 7, 2, 0);
        top.Controls.Add(_locationLabel);
        _locationBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _locationBox.Width = 190;
        _locationBox.Margin = new Padding(0, 3, 16, 3);
        _locationBox.SelectedIndexChanged += (_, _) => RebuildList();
        top.Controls.Add(_locationBox);

        _sceneLabel.Text = "场景：";
        _sceneLabel.AutoSize = true;
        _sceneLabel.Margin = new Padding(0, 7, 2, 0);
        top.Controls.Add(_sceneLabel);
        _sceneBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _sceneBox.Width = 190;
        _sceneBox.Margin = new Padding(0, 3, 16, 3);
        foreach (var scene in MenuScenes.All)
        {
            _sceneBox.Items.Add(scene.DisplayName());
        }

        _sceneBox.SelectedIndex = MenuScenes.All.ToList().IndexOf(_scene);
        top.Controls.Add(_sceneBox);

        top.Controls.Add(new Label { Text = "搜索：", AutoSize = true, Margin = new Padding(0, 7, 2, 0) });
        _searchBox.Width = 210;
        _searchBox.Margin = new Padding(0, 3, 16, 3);
        _searchBox.TextChanged += (_, _) => RebuildList();
        _searchBox.HandleCreated += (_, _) => NativeMethods.SetCueBanner(_searchBox, "搜索名称 / 命令 / 来源");
        top.Controls.Add(_searchBox);

        _onlyDisabled.Text = "只看已禁用";
        _onlyDisabled.AutoSize = true;
        _onlyDisabled.Margin = new Padding(0, 7, 16, 3);
        _onlyDisabled.CheckedChanged += (_, _) => RebuildList();
        top.Controls.Add(_onlyDisabled);

        top.Controls.Add(CreateButton("刷新", (_, _) => RefreshEntries()));
        top.Controls.Add(CreateButton("重启资源管理器", (_, _) => RestartExplorer()));
        top.Controls.Add(CreateButton("操作历史", (_, _) => ShowHistory()));
        top.Controls.Add(CreateButton("关于", (_, _) => ShowAbout()));

        _classicMenuBox.Text = "经典右键菜单（实验）";
        _classicMenuBox.AutoSize = true;
        _classicMenuBox.Margin = new Padding(10, 7, 8, 3);
        _classicMenuBox.CheckedChanged += (_, _) => OnClassicMenuCheckedChanged();
        top.Controls.Add(_classicMenuBox);

        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.CheckBoxes = true;
        _list.HideSelection = false;
        _list.ShowItemToolTips = true;
        _imageList.ImageSize = new Size(16, 16);
        _imageList.ColorDepth = ColorDepth.Depth32Bit;
        _list.SmallImageList = _imageList;
        _list.ItemChecked += OnItemChecked;
        _list.ItemActivate += OnItemActivate;
        RebuildColumns();

        var statusBar = new StatusStrip();
        _statusLabel.Spring = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        statusBar.Items.Add(_statusLabel);

        Controls.Add(_list);
        Controls.Add(top);
        Controls.Add(statusBar);

        InitFilters();

        _modeBox.SelectedIndexChanged += (_, _) => OnModeChanged();
        _sceneBox.SelectedIndexChanged += (_, _) => OnSceneChanged();
        ApplyModeVisibility();
    }

    private void OnModeChanged()
    {
        _simpleMode = _modeBox.SelectedIndex == 0;
        ApplyModeVisibility();
        RebuildColumns();
        RebuildList();
        SaveSettings();
    }

    private void OnSceneChanged()
    {
        var index = _sceneBox.SelectedIndex;
        if (index < 0 || index >= MenuScenes.All.Count)
        {
            return;
        }

        _scene = MenuScenes.All[index];
        RebuildList();
        SaveSettings();
    }

    private void ApplyModeVisibility()
    {
        _locationLabel.Visible = !_simpleMode;
        _locationBox.Visible = !_simpleMode;
        _sceneLabel.Visible = _simpleMode;
        _sceneBox.Visible = _simpleMode;
        _onlyDisabled.Text = _simpleMode ? "只看已隐藏" : "只看已禁用";
    }

    private void SaveSettings() => _settings.Update(_simpleMode, _scene);

    private void RebuildColumns()
    {
        _list.Columns.Clear();
        if (_simpleMode)
        {
            _list.Columns.Add("菜单项", 330);
            _list.Columns.Add("状态", 90);
            _list.Columns.Add("来源", 230);
            _list.Columns.Add("说明", 200);
            return;
        }

        _list.Columns.Add("名称", 260);
        _list.Columns.Add("状态", 80);
        _list.Columns.Add("类型", 100);
        _list.Columns.Add("位置", 120);
        _list.Columns.Add("作用域", 100);
        _list.Columns.Add("来源", 150);
        _list.Columns.Add("命令 / CLSID", 300);
    }

    private Button CreateButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 2, 8, 2),
            Padding = new Padding(6, 2, 6, 2),
        };
        button.Click += onClick;
        return button;
    }

    private void InitFilters()
    {
        _locationBox.Items.AddRange(
        [
            new LocationFilter("全部位置", _ => true),
            new LocationFilter("所有文件 (*)", entry => entry.Location == LocationKind.AllFiles),
            new LocationFilter("文件夹", entry => entry.Location == LocationKind.Directory),
            new LocationFilter("文件夹背景", entry => entry.Location == LocationKind.DirectoryBackground),
            new LocationFilter("驱动器", entry => entry.Location == LocationKind.Drive),
            new LocationFilter("桌面背景", entry => entry.Location == LocationKind.DesktopBackground),
            new LocationFilter("所有文件系统对象", entry => entry.Location == LocationKind.AllFilesystemObjects),
            new LocationFilter("文件夹（通用）", entry => entry.Location == LocationKind.Folder),
            new LocationFilter("COM 扩展", entry => entry.Kind == EntryKind.ComHandler && !entry.IsOrphan),
            new LocationFilter("已屏蔽残留", entry => entry.IsOrphan),
        ]);
        _locationBox.SelectedIndex = 0;
    }

    private void RefreshEntries()
    {
        UseWaitCursor = true;
        try
        {
            _entries.Clear();
            _entries.AddRange(_scanner.ScanAll());
            RebuildList();
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void RebuildList()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        if (_simpleMode)
        {
            RebuildSimpleList();
            return;
        }

        RebuildAdvancedList();
    }

    private void RebuildSimpleList()
    {
        IEnumerable<SimpleMenuItem> query = SimpleMenuBuilder.Build(_entries, _scene);

        if (_onlyDisabled.Checked)
        {
            query = query.Where(item => !item.IsShown);
        }

        var text = _searchBox.Text.Trim();
        if (text.Length > 0)
        {
            query = query.Where(item => MatchesSimple(item, text));
        }

        var ordered = query.ToArray();

        _suppressCheck = true;
        _list.BeginUpdate();
        _list.Items.Clear();
        _list.Items.AddRange(ordered.Select(CreateSimpleItem).ToArray());
        _list.EndUpdate();
        _suppressCheck = false;

        var hidden = ordered.Count(item => !item.IsShown);
        var classic = ClassicMenuService.IsEnabled() ? "已开启" : "未开启";
        _statusLabel.Text = $"场景：{_scene.DisplayName()} · 共 {ordered.Length} 项 · 已隐藏 {hidden} 项 · 经典菜单：{classic}（实验）";
    }

    private static bool MatchesSimple(SimpleMenuItem item, string text) =>
        item.DisplayName.Contains(text, StringComparison.CurrentCultureIgnoreCase)
        || (item.Publisher?.Contains(text, StringComparison.CurrentCultureIgnoreCase) ?? false)
        || item.Sources.Any(source => source.RegistryPath.Contains(text, StringComparison.OrdinalIgnoreCase));

    private void RebuildAdvancedList()
    {
        var filter = _locationBox.SelectedItem as LocationFilter ?? new LocationFilter("全部位置", _ => true);
        IEnumerable<MenuEntry> query = _entries.Where(filter.Match);

        if (_onlyDisabled.Checked)
        {
            query = query.Where(entry => entry.State != EntryState.Enabled);
        }

        var text = _searchBox.Text.Trim();
        if (text.Length > 0)
        {
            query = query.Where(entry => Matches(entry, text));
        }

        var ordered = query
            .OrderBy(entry => entry.Location)
            .ThenBy(entry => entry.Kind)
            .ThenBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        _suppressCheck = true;
        _list.BeginUpdate();
        _list.Items.Clear();
        _list.Items.AddRange(ordered.Select(CreateItem).ToArray());
        _list.EndUpdate();
        _suppressCheck = false;

        var disabled = ordered.Count(entry => entry.State == EntryState.Disabled);
        var orphans = ordered.Count(entry => entry.IsOrphan);
        var classic = ClassicMenuService.IsEnabled() ? "已开启" : "未开启";
        _statusLabel.Text = $"共 {ordered.Length} 项 · 已禁用 {disabled} 项 · 屏蔽残留 {orphans} 项 · 经典菜单：{classic}（实验）";
    }

    private static bool Matches(MenuEntry entry, string text)
    {
        return entry.DisplayName.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || (entry.Publisher?.Contains(text, StringComparison.CurrentCultureIgnoreCase) ?? false)
            || (entry.Command?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
            || (entry.Clsid?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
            || entry.RegistryPath.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private ListViewItem CreateSimpleItem(SimpleMenuItem item)
    {
        var primary = item.Sources[0];
        var row = new ListViewItem(item.DisplayName)
        {
            Tag = item,
            Checked = item.IsShown,
            ToolTipText = item.IsUnsupported
                ? item.NoteText
                : string.Join(Environment.NewLine, item.Sources.Select(source => source.RegistryPath).Distinct()),
        };

        ApplyIcon(row, primary);
        row.SubItems.Add(item.StateText);
        row.SubItems.Add(item.Publisher ?? "—");
        row.SubItems.Add(item.NoteText);

        if (!item.IsShown)
        {
            row.ForeColor = Color.Gray;
        }
        else if (item.IsUnsupported || item.IsPartiallyHidden)
        {
            row.ForeColor = Color.FromArgb(140, 140, 140);
        }

        return row;
    }

    private void ApplyIcon(ListViewItem row, MenuEntry entry)
    {
        var icon = _icons.ForEntry(entry);
        if (icon == null)
        {
            return;
        }

        if (!_imageList.Images.ContainsKey(entry.Id))
        {
            _imageList.Images.Add(entry.Id, icon);
        }

        row.ImageKey = entry.Id;
    }

    private ListViewItem CreateItem(MenuEntry entry)
    {
        var item = new ListViewItem(entry.DisplayName)
        {
            Tag = entry,
            Checked = entry.State == EntryState.Enabled,
            ToolTipText = entry.RegistryPath,
        };

        ApplyIcon(item, entry);

        item.SubItems.Add(entry.StateText);
        item.SubItems.Add(entry.KindText + (entry.IsSystem ? "·系统" : string.Empty));
        item.SubItems.Add(RegistryLocations.DisplayNameFor(entry.Location));
        item.SubItems.Add(entry.ScopeText);
        item.SubItems.Add(entry.Publisher ?? "—");
        item.SubItems.Add(Shorten(entry.Command, 120));

        if (entry.State == EntryState.Disabled)
        {
            item.ForeColor = Color.Gray;
        }
        else if (!entry.CanToggle)
        {
            item.ForeColor = Color.FromArgb(140, 140, 140);
        }

        return item;
    }

    private static string Shorten(string? value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "—";
        }

        var text = value.Trim();
        return text.Length <= limit ? text : text[..(limit - 1)] + "…";
    }

    private void OnItemChecked(object? sender, ItemCheckedEventArgs e)
    {
        if (_suppressCheck)
        {
            return;
        }

        if (e.Item.Tag is SimpleMenuItem simple)
        {
            var wantShown = e.Item.Checked;
            e.Item.Checked = simple.IsShown;

            if (simple.IsUnsupported)
            {
                MessageBox.Show(this, "该菜单项是新型命令（ExplorerCommand），当前版本暂不支持隐藏，已列入后续计划。", "右键菜单编辑器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (wantShown == simple.IsShown)
            {
                return;
            }

            ApplySimpleToggle(simple, hide: !wantShown);
            return;
        }

        if (e.Item.Tag is not MenuEntry entry)
        {
            return;
        }

        var wantsEnabled = e.Item.Checked;
        e.Item.Checked = entry.State == EntryState.Enabled;

        if (entry.IsOrphan)
        {
            CleanOrphan(entry);
            return;
        }

        if (!entry.CanToggle)
        {
            var message = entry.Kind == EntryKind.ExplorerCommand
                ? "该菜单项是新型命令（ExplorerCommand），当前版本暂不支持禁用，已列入后续计划。"
                : "该菜单项缺少可识别的标识，当前无法操作。";
            MessageBox.Show(this, message, "右键菜单编辑器", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (wantsEnabled == (entry.State == EntryState.Enabled))
        {
            return;
        }

        ApplyToggle(entry, disable: !wantsEnabled);
    }

    private void ApplySimpleToggle(SimpleMenuItem item, bool hide)
    {
        var title = $"{(hide ? "隐藏" : "显示")}「{item.DisplayName}」？";
        var lines = new List<string>
        {
            $"场景：{_scene.DisplayName()}",
            hide ? "将把它从右键菜单中隐藏，之后可随时恢复。" : "将让它重新出现在右键菜单中。",
        };

        if (item.Sources.Count > 1)
        {
            lines.Add($"该菜单项在系统中有 {item.Sources.Count} 处注册，将一并处理。");
        }

        var warning = item.IsSystem ? "这是系统关键项，隐藏后可能影响文件夹的正常打开操作。" : null;

        if (!ConfirmDialog.Show(this, title, string.Join(Environment.NewLine, lines), warning, hide ? "确认隐藏" : "确认显示", _icons.ForEntry(item.Sources[0])))
        {
            return;
        }

        RunToggleMany(item, hide ? "disable" : "enable");
    }

    private void ShowSimpleDetail(SimpleMenuItem item)
    {
        var representative = item.ToggleRepresentatives().FirstOrDefault() ?? item.Sources[0];
        var note = item.Sources.Count > 1
            ? $"该菜单项在系统中共有 {item.Sources.Count} 处注册，这里显示的是其中一处；隐藏时会一并处理。"
            : null;

        if (!DetailDialog.Show(this, representative, _icons.ForEntry(representative), note) || item.IsUnsupported)
        {
            return;
        }

        ApplySimpleToggle(item, hide: item.IsShown);
    }

    private void RunToggleMany(SimpleMenuItem item, string action)
    {
        try
        {
            foreach (var entry in item.ToggleRepresentatives())
            {
                var target = entry.ToToggleTarget();
                if (action == "disable")
                {
                    _toggles.Disable(target);
                }
                else
                {
                    _toggles.Enable(target);
                }

                _journal.Append(JournalRecord.FromToggle(target, action));
            }

            ExplorerService.NotifyShellChanged();
        }
        catch (Exception exception)
        {
            var message = exception is UnauthorizedAccessException or System.Security.SecurityException
                ? "没有权限修改该注册表项，请以管理员身份运行。"
                : exception.Message;
            MessageBox.Show(this, "操作失败：" + message, "右键菜单编辑器", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            RefreshEntries();
        }
    }

    private void ApplyToggle(MenuEntry entry, bool disable)
    {
        var title = $"{(disable ? "禁用" : "恢复显示")}「{entry.DisplayName}」？";
        var lines = new List<string>
        {
            $"位置：{RegistryLocations.DisplayNameFor(entry.Location)} · 作用域：{entry.ScopeText}",
            $"注册表：{entry.RegistryPath}",
            disable ? "将隐藏该右键菜单项，之后可随时在此恢复。" : "将重新显示该右键菜单项。",
        };

        var warning = entry.IsSystem
            ? "这是系统关键项，禁用可能影响文件夹的正常打开操作。"
            : null;

        if (!ConfirmDialog.Show(this, title, string.Join(Environment.NewLine, lines), warning, disable ? "确认禁用" : "确认恢复", _icons.ForEntry(entry)))
        {
            return;
        }

        RunToggle(entry.ToToggleTarget(), disable ? "disable" : "enable");
    }

    private void CleanOrphan(MenuEntry entry)
    {
        var detail = $"屏蔽列表中残留的记录（未找到对应程序）：{Environment.NewLine}{entry.Clsid}";
        if (!ConfirmDialog.Show(this, "清理残留屏蔽记录？", detail, null, "确认清理", _icons.ForEntry(entry)))
        {
            return;
        }

        RunToggle(entry.ToToggleTarget(), "clean");
    }

    private void RunToggle(ToggleTarget target, string action)
    {
        try
        {
            switch (action)
            {
                case "disable":
                    _toggles.Disable(target);
                    break;
                case "enable":
                    _toggles.Enable(target);
                    break;
                default:
                    _toggles.CleanOrphan(target);
                    break;
            }

            _journal.Append(JournalRecord.FromToggle(target, action));
            ExplorerService.NotifyShellChanged();
        }
        catch (Exception exception)
        {
            var message = exception is UnauthorizedAccessException or System.Security.SecurityException
                ? "没有权限修改该注册表项，请以管理员身份运行。"
                : exception.Message;
            MessageBox.Show(this, "操作失败：" + message, "右键菜单编辑器", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            RefreshEntries();
        }
    }

    private void OnItemActivate(object? sender, EventArgs e)
    {
        if (_list.SelectedItems.Count == 0)
        {
            return;
        }

        if (_list.SelectedItems[0].Tag is SimpleMenuItem simple)
        {
            ShowSimpleDetail(simple);
            return;
        }

        if (_list.SelectedItems[0].Tag is not MenuEntry entry)
        {
            return;
        }

        if (!DetailDialog.Show(this, entry, _icons.ForEntry(entry)))
        {
            return;
        }

        if (entry.IsOrphan)
        {
            CleanOrphan(entry);
        }
        else if (entry.CanToggle)
        {
            ApplyToggle(entry, disable: entry.State == EntryState.Enabled);
        }
    }

    private void RestartExplorer()
    {
        var answer = MessageBox.Show(
            this,
            "将重启 Windows 资源管理器，任务栏会短暂消失后恢复。继续？",
            "重启资源管理器",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        UseWaitCursor = true;
        try
        {
            ExplorerService.RestartExplorer();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, "重启失败：" + exception.Message, "右键菜单编辑器", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ShowHistory()
    {
        if (HistoryDialog.Show(this, _journal))
        {
            RefreshEntries();
            RefreshClassicMenuState();
        }
    }

    private void RefreshClassicMenuState()
    {
        _suppressClassic = true;
        _classicMenuBox.Checked = ClassicMenuService.IsEnabled();
        _suppressClassic = false;
    }

    private void OnClassicMenuCheckedChanged()
    {
        if (_suppressClassic)
        {
            return;
        }

        var desired = _classicMenuBox.Checked;
        RefreshClassicMenuState();
        if (desired == ClassicMenuService.IsEnabled())
        {
            return;
        }

        var title = desired ? "开启经典右键菜单？" : "关闭经典右键菜单？";
        var detail = desired
            ? "将恢复 Windows 10 风格的完整右键菜单，不必再点“显示更多选项”。"
            : "将恢复 Windows 11 新式右键菜单，扩展项需点“显示更多选项”查看。";

        if (!ConfirmDialog.Show(this, title, detail, "该开关为实验性功能，通过注册表 CLSID 覆盖实现。", desired ? "确认开启" : "确认关闭", null))
        {
            return;
        }

        try
        {
            if (desired)
            {
                ClassicMenuService.Enable();
            }
            else
            {
                ClassicMenuService.Disable();
            }

            _journal.Append(JournalRecord.ForClassicMenu(desired ? "classic-on" : "classic-off"));
            ExplorerService.NotifyShellChanged();
            MessageBox.Show(this, "已切换。若未立即生效，请点击“重启资源管理器”。", "右键菜单编辑器", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, "操作失败：" + exception.Message, "右键菜单编辑器", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            RefreshClassicMenuState();
        }
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            this,
            $"右键菜单编辑器 v{Application.ProductVersion}" + Environment.NewLine + Environment.NewLine +
            "非破坏性地禁用与恢复资源管理器右键菜单项：" + Environment.NewLine +
            "· 静态菜单项：写入 LegacyDisable 值（可随时删除还原）" + Environment.NewLine +
            "· COM 扩展：写入 Shell Extensions\\Blocked 屏蔽列表" + Environment.NewLine +
            "· 所有操作都会记录到 %APPDATA%\\ContextMenuEditor\\journal.ndjson" + Environment.NewLine + Environment.NewLine +
            "https://github.com/heathacosta259519-dot/akagi-toolbox",
            "关于",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private sealed class LocationFilter(string text, Func<MenuEntry, bool> match)
    {
        public string Text { get; } = text;
        public Func<MenuEntry, bool> Match { get; } = match;

        public override string ToString() => Text;
    }
}
