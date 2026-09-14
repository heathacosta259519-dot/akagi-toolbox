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
    private readonly MenuProbeRunner _probeRunner;
    private readonly IconService _icons = new();
    private readonly ImageList _imageList = new();
    private readonly List<MenuEntry> _entries = [];
    private readonly ComboBox _modeBox = new();
    private readonly ComboBox _locationBox = new();
    private readonly FlowLayoutPanel _sceneTabs = new();
    private readonly Label _locationLabel = new();
    private readonly Label _targetLabel = new();
    private readonly TextBox _targetBox = new();
    private Button? _browseButton;
    private string _fileTarget = string.Empty;
    private string _folderTarget = string.Empty;
    private int _replicaGeneration;
    private int _listNotificationDepth;
    private bool _handlingItemEvent;
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
        _probeRunner = new MenuProbeRunner(Environment.ProcessPath ?? Application.ExecutablePath);
        _simpleMode = _settings.SimpleMode;
        _scene = _settings.Scene;
        _fileTarget = _settings.FileTarget;
        _folderTarget = _settings.FolderTarget;
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

        _sceneTabs.Dock = DockStyle.Top;
        _sceneTabs.AutoSize = true;
        _sceneTabs.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _sceneTabs.WrapContents = true;
        _sceneTabs.FlowDirection = FlowDirection.LeftToRight;
        _sceneTabs.Padding = new Padding(10, 0, 10, 8);
        foreach (var scene in MenuScenes.All)
        {
            var tab = new RadioButton
            {
                Text = scene.DisplayName(),
                Appearance = Appearance.Button,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Size = new Size(110, 30),
                Tag = scene,
                Checked = scene == _scene,
                Margin = new Padding(0, 0, 4, 0),
            };
            tab.CheckedChanged += (_, _) =>
            {
                if (tab.Checked)
                {
                    OnSceneChanged(scene);
                }
            };
            _sceneTabs.Controls.Add(tab);
        }

        _targetLabel.Text = "示例目标：";
        _targetLabel.AutoSize = true;
        _targetLabel.Margin = new Padding(0, 7, 2, 0);
        top.Controls.Add(_targetLabel);

        _targetBox.Width = 460;
        _targetBox.Margin = new Padding(0, 3, 4, 3);
        _targetBox.KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Enter)
            {
                args.SuppressKeyPress = true;
                ApplyTargetFromBox();
            }
        };
        top.Controls.Add(_targetBox);

        _browseButton = CreateButton("浏览…", (_, _) => BrowseTarget());
        top.Controls.Add(_browseButton);

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
        Controls.Add(_sceneTabs);
        Controls.Add(top);
        Controls.Add(statusBar);

        InitFilters();

        _modeBox.SelectedIndexChanged += (_, _) => OnModeChanged();
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

    private void OnSceneChanged(MenuScene scene)
    {
        _scene = scene;
        RebuildList();
        SaveSettings();
    }

    private void ApplyModeVisibility()
    {
        _locationLabel.Visible = !_simpleMode;
        _locationBox.Visible = !_simpleMode;
        _sceneTabs.Visible = _simpleMode;
        _targetLabel.Visible = _simpleMode;
        _targetBox.Visible = _simpleMode;
        if (_browseButton != null)
        {
            _browseButton.Visible = _simpleMode;
        }

        _onlyDisabled.Text = _simpleMode ? "只看已隐藏" : "只看已禁用";
    }

    private void SaveSettings() => _settings.Update(_simpleMode, _scene, _fileTarget, _folderTarget);

    private void ApplyTargetFromBox()
    {
        var path = _targetBox.Text.Trim().Trim('"');
        if (path.Length == 0)
        {
            return;
        }

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            MessageBox.Show(this, "路径不存在：" + path, "右键菜单编辑器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (Directory.Exists(path))
        {
            _folderTarget = path;
        }
        else
        {
            _fileTarget = path;
        }

        SaveSettings();
        _probeRunner.Invalidate();
        RebuildList();
    }

    private void BrowseTarget()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择示例目标（文件；文件夹可直接在输入框里粘贴路径）",
            Filter = "所有文件|*.*",
            CheckFileExists = true,
        };

        var current = CurrentTarget();
        if (File.Exists(current))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(current);
            dialog.FileName = Path.GetFileName(current);
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _targetBox.Text = dialog.FileName;
            ApplyTargetFromBox();
        }
    }

    private string CurrentTarget()
    {
        switch (_scene)
        {
            case MenuScene.Files:
                return EnsureTarget(ref _fileTarget, isFolder: false);
            case MenuScene.DesktopBackground:
                return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            case MenuScene.Drive:
                var drive = DriveInfo.GetDrives().FirstOrDefault(item => item.IsReady) ?? DriveInfo.GetDrives().FirstOrDefault();
                return drive?.RootDirectory.FullName ?? "C:\\";
            default:
                return EnsureTarget(ref _folderTarget, isFolder: true);
        }
    }

    private string EnsureTarget(ref string target, bool isFolder)
    {
        if (!string.IsNullOrWhiteSpace(target) && (isFolder ? Directory.Exists(target) : File.Exists(target)))
        {
            return target;
        }

        var directory = Path.Combine(Path.GetTempPath(), "ContextMenuEditor");
        Directory.CreateDirectory(directory);
        target = isFolder
            ? Directory.CreateDirectory(Path.Combine(directory, "示例文件夹")).FullName
            : CreateSampleFile(Path.Combine(directory, "示例文件.txt"));
        return target;
    }

    private static string CreateSampleFile(string path)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "右键菜单编辑器的示例文件，可以把它改成你常右键的文件类型。" + Environment.NewLine);
        }

        return path;
    }

    private void RebuildColumns()
    {
        _list.Columns.Clear();
        _list.ShowGroups = false;

        if (_simpleMode)
        {
            _list.Columns.Add("菜单项", 420);
            _list.Columns.Add("状态", 90);
            _list.Columns.Add("归属", 200);
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
        if (IsDisposed)
        {
            return;
        }

        UseWaitCursor = true;
        try
        {
            _probeRunner.Invalidate();
            _entries.Clear();
            _entries.AddRange(_scanner.ScanAll());
            RebuildList();
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void RequestRefresh()
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(new Action(RefreshEntries));
    }

    private void RebuildList()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        if (_listNotificationDepth > 0)
        {
            RequestRefresh();
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
        var items = SimpleMenuBuilder.Build(_entries, _scene);
        var target = CurrentTarget();
        if (!string.Equals(_targetBox.Text, target, StringComparison.OrdinalIgnoreCase))
        {
            _targetBox.Text = target;
        }

        var clsids = items
            .SelectMany(item => item.Sources)
            .Where(source => source.Kind == EntryKind.ComHandler && source.Clsid != null)
            .Select(source => source.Clsid!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var commandClsids = items
            .SelectMany(item => item.Sources)
            .Where(source => source.Kind == EntryKind.ExplorerCommand && source.Clsid != null)
            .Select(source => source.Clsid!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var menu = _probeRunner.TryGetMenu(_scene, target);
        if (menu != null)
        {
            RenderSimpleList(items, target, menu, _probeRunner.TryGetHandlerMap(_scene, target, clsids, commandClsids), loading: false);
            return;
        }

        RenderSimpleList(items, target, null, null, loading: true);
        StartReplicaLoad(_scene, target, clsids, commandClsids);
    }

    private void StartReplicaLoad(MenuScene scene, string target, string[] clsids, string[] commandClsids)
    {
        var generation = ++_replicaGeneration;

        Task.Run(() =>
        {
            var menu = _probeRunner.GetMenu(scene, target);
            PostReplica(generation, scene, target, menu, null);

            if (menu is { Error: null })
            {
                var map = _probeRunner.GetHandlerMap(scene, target, clsids, commandClsids);
                PostReplica(generation, scene, target, menu, map);
            }
        });
    }

    private void PostReplica(int generation, MenuScene scene, string target, MenuProbeResult menu, Dictionary<string, List<string>>? handlerMap)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(new Action(() =>
            {
                if (generation != _replicaGeneration || scene != _scene || !string.Equals(target, CurrentTarget(), StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (_listNotificationDepth > 0)
                {
                    PostReplica(generation, scene, target, menu, handlerMap);
                    return;
                }

                var items = SimpleMenuBuilder.Build(_entries, _scene);
                RenderSimpleList(items, target, menu, handlerMap, loading: handlerMap == null && menu.Error == null);
            }));
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void RenderSimpleList(
        IReadOnlyList<SimpleMenuItem> items,
        string target,
        MenuProbeResult? menu,
        IReadOnlyDictionary<string, List<string>>? handlerMap,
        bool loading)
    {
        var search = _searchBox.Text.Trim();
        var onlyHidden = _onlyDisabled.Checked;
        var hasMenu = menu is { Error: null, Items.Count: > 0 };

        var menuCount = 0;
        var hiddenCount = 0;
        var owners = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

        _suppressCheck = true;
        _list.BeginUpdate();
        _list.Items.Clear();
        _list.Groups.Clear();

        if (loading)
        {
            _list.Items.Add(new ListViewItem("正在构建真实菜单…（后台进行，第三方扩展在独立进程里运行）")
            {
                ForeColor = Color.FromArgb(120, 120, 120),
            });
        }

        if (hasMenu)
        {
            if (!onlyHidden)
            {
                foreach (var row in MenuReplicaBuilder.Build(menu!, items, handlerMap ?? new Dictionary<string, List<string>>()))
                {
                    if (search.Length > 0 && !MatchesRow(row, search))
                    {
                        continue;
                    }

                    AddReplicaRow(row);
                    if (row.IsSeparator)
                    {
                        continue;
                    }

                    menuCount++;
                    if (row.Owner != null)
                    {
                        owners.Add(row.OwnerName);
                    }
                }
            }

            var hiddenItems = items
                .Where(item => !item.IsShown)
                .Where(item => search.Length == 0 || MatchesSimple(item, search))
                .ToArray();

            if (hiddenItems.Length > 0 && !onlyHidden && menuCount > 0)
            {
                _list.Items.Add(new ListViewItem("──────") { Tag = null, ForeColor = Color.Silver });
            }

            foreach (var item in hiddenItems)
            {
                AddRegistryRow(item);
                hiddenCount++;
                owners.Add(item.Owner);
            }
        }
        else
        {
            var fallback = items
                .Where(item => !onlyHidden || !item.IsShown)
                .Where(item => search.Length == 0 || MatchesSimple(item, search))
                .ToArray();

            foreach (var item in fallback)
            {
                AddRegistryRow(item);
                menuCount++;
                owners.Add(item.Owner);
                if (!item.IsShown)
                {
                    hiddenCount++;
                }
            }
        }

        _list.EndUpdate();
        _suppressCheck = false;

        var classic = ClassicMenuService.IsEnabled() ? "已开启" : "未开启";
        var source = loading ? "正在构建真实菜单" : hasMenu ? "真实菜单" : "注册表视图（探测未成功）";
        _statusLabel.Text = $"{_scene.DisplayName()}右键 · {source} · 菜单 {menuCount} 项 · 已隐藏 {hiddenCount} 项 · 归属 {owners.Count} 个程序 · 目标：{target} · 经典菜单：{classic}（实验）";
    }

    private static bool MatchesRow(MenuReplicaRow row, string text) =>
        row.Text.Contains(text, StringComparison.CurrentCultureIgnoreCase)
        || row.OwnerName.Contains(text, StringComparison.CurrentCultureIgnoreCase);

    private void AddReplicaRow(MenuReplicaRow row)
    {
        if (row.IsSeparator)
        {
            _list.Items.Add(new ListViewItem("──────") { Tag = row, ForeColor = Color.Silver });
            return;
        }

        var entry = new ListViewItem(row.Text)
        {
            Tag = row,
            Checked = true,
            IndentCount = Math.Min(row.Depth, 4),
            ToolTipText = row.Owner != null && row.Owner.Sources.Count > 0
                ? row.Owner.Sources[0].RegistryPath
                : "这一项在注册表里没有对应的开关：可能是 Windows 自带的菜单项，或由程序在运行时生成。",
        };

        if (row.Owner != null && row.Owner.Sources.Count > 0)
        {
            ApplyIcon(entry, row.Owner.Sources[0]);
        }

        entry.SubItems.Add(row.CanToggle ? "显示中" : row.Owner == null ? "无开关" : "暂不支持");
        entry.SubItems.Add(row.OwnerName);

        if (!row.IsEnabled)
        {
            entry.ForeColor = Color.Gray;
        }
        else if (!row.CanToggle)
        {
            entry.ForeColor = Color.FromArgb(140, 140, 140);
        }

        _list.Items.Add(entry);
    }

    private void AddRegistryRow(SimpleMenuItem item)
    {
        _list.Items.Add(CreateSimpleItem(item));
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
        var row = new ListViewItem(item.HasChildren ? item.DisplayName + "  ▸" : item.DisplayName)
        {
            Tag = item,
            Checked = item.IsShown,
            IndentCount = Math.Min(item.Indent, 4),
            ToolTipText = item.IsUnsupported
                ? item.NoteText
                : string.Join(Environment.NewLine, item.Sources.Select(source => source.RegistryPath).Distinct()),
        };

        ApplyIcon(row, primary);
        row.SubItems.Add(item.StateText);
        row.SubItems.Add(item.Owner);

        if (!item.IsShown || item.IsAncestorHidden)
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
        if (_suppressCheck || _handlingItemEvent)
        {
            return;
        }

        _handlingItemEvent = true;
        _listNotificationDepth++;
        try
        {
            HandleItemChecked(e);
        }
        finally
        {
            _listNotificationDepth--;
            _handlingItemEvent = false;
        }
    }

    private void HandleItemChecked(ItemCheckedEventArgs e)
    {
        if (e.Item.Tag is MenuReplicaRow replicaRow)
        {
            e.Item.Checked = true;

            if (replicaRow.IsSeparator)
            {
                return;
            }

            if (replicaRow.Owner == null)
            {
                MessageBox.Show(this, "「" + replicaRow.Text + "」在注册表里没有对应的开关，无法隐藏（通常是 Windows 自带的项，或由程序在运行时生成）。", "右键菜单编辑器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!replicaRow.CanToggle)
            {
                MessageBox.Show(this, "「" + replicaRow.Text + "」没有可用的注册表开关，无法隐藏。", "右键菜单编辑器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ApplySimpleToggle(replicaRow.Owners, replicaRow.Text, hide: true);
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

    private void ApplySimpleToggle(SimpleMenuItem item, bool hide) => ApplySimpleToggle([item], item.DisplayName, hide);

    private void ApplySimpleToggle(IReadOnlyList<SimpleMenuItem> owners, string displayName, bool hide)
    {
        var title = $"{(hide ? "隐藏" : "显示")}「{displayName}」？";
        var lines = new List<string>
        {
            $"场景：{_scene.DisplayName()}",
            hide ? "将把它从右键菜单中隐藏，之后可随时恢复。" : "将让它重新出现在右键菜单中。",
        };

        var programs = string.Join(" / ", owners.Select(owner => owner.Owner).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.CurrentCultureIgnoreCase));
        var registrations = owners.Sum(owner => owner.Sources.Count);
        if (owners.Count > 1)
        {
            lines.Add($"该菜单项由 {owners.Count} 个程序提供（{programs}），将一并隐藏。");
        }
        else if (registrations > 1)
        {
            lines.Add($"该菜单项在系统中有 {registrations} 处注册，将一并处理。");
        }

        var warning = owners.Any(owner => owner.IsSystem) ? "这是系统关键项，隐藏后可能影响文件夹的正常打开操作。" : null;

        if (!ConfirmDialog.Show(this, title, string.Join(Environment.NewLine, lines), warning, hide ? "确认隐藏" : "确认显示", _icons.ForEntry(owners[0].Sources[0])))
        {
            return;
        }

        RunToggleMany(owners, hide ? "disable" : "enable");
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

    private void RunToggleMany(IReadOnlyList<SimpleMenuItem> owners, string action)
    {
        try
        {
            foreach (var owner in owners)
            {
                foreach (var entry in owner.ToggleRepresentatives())
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
            RequestRefresh();
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
            RequestRefresh();
        }
    }

    private void OnItemActivate(object? sender, EventArgs e)
    {
        if (_handlingItemEvent)
        {
            return;
        }

        _handlingItemEvent = true;
        _listNotificationDepth++;
        try
        {
            HandleItemActivate();
        }
        finally
        {
            _listNotificationDepth--;
            _handlingItemEvent = false;
        }
    }

    private void HandleItemActivate()
    {
        if (_list.SelectedItems.Count == 0)
        {
            return;
        }

        if (_list.SelectedItems[0].Tag is MenuReplicaRow replicaRow)
        {
            if (replicaRow.IsSeparator)
            {
                return;
            }

            if (replicaRow.Owner != null)
            {
                ShowSimpleDetail(replicaRow.Owner);
            }
            else
            {
                MessageBox.Show(this, "「" + replicaRow.Text + "」是 Windows 自带的内置菜单项，没有独立的注册表开关。", "右键菜单编辑器", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

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
