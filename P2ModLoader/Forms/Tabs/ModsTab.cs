using System.Diagnostics;
using System.Text;
using P2ModLoader.Abstract;
using P2ModLoader.Data;
using P2ModLoader.Helper;
using P2ModLoader.ModList;
using P2ModLoader.WindowsFormsExtensions;

namespace P2ModLoader.Forms.Tabs;

public class ModsTab : BaseTab {
    private ListView? _modListView;
    private TextBox? _descriptionBox;
    private TableLayoutPanel? _mainContainer;
    private TableLayoutPanel? _messageContainer;
    private Label? _messageLabel;
    private Button? _initializeButton;
    private bool _isRefreshing;

    public event Action? ModsChanged;

    public ModsTab(TabPage page) : base(page) {
        _isRefreshing = false;

        InitializeComponents();
        InitializeEvents();

        ModManager.ModsLoaded += OnModsLoaded;

        SettingsHolder.InstallPathChanged += OnSettingChanged;
        SettingsHolder.StartupWithConflictsChanged += OnSettingChanged;

        ModsChanged += () => {
            SettingsHolder.IsPatched = false;
        };

        SettingsHolder.InstallPathChanged += UpdateUIState;
        UpdateUIState();
    }

    public bool HasFileConflicts() {
        var allMods = ModManager.Mods.ToList();
        return allMods.Any(mod => {
            var display = ConflictManager.GetConflictDisplay(mod, allMods);
            return display.BackgroundColor == Color.LightCoral;
        });
    }

    private void OnSettingChanged() {
        if (_modListView?.InvokeRequired != true) return;
        _modListView.Invoke(OnSettingChanged);
    }

    private void OnModsLoaded() {
        if (_modListView!.InvokeRequired) {
            _modListView.Invoke(RefreshModList);
        }
        else {
            RefreshModList();
        }
    }

    private void UpdateUIState() {
        if (_mainContainer == null) return;

        var installPath = SettingsHolder.InstallPath;

        if (string.IsNullOrEmpty(installPath)) {
            ShowMessage("Head to Settings to specify the install path.", false);
            return;
        }

        var modsPath = Path.Combine(installPath, "Mods");
        if (!Directory.Exists(modsPath)) {
            ShowMessage(
                "P2ModLoader has not been initialized in this directory yet. Press \"Initialize\" to generate the necessary folders.",
                true);
            return;
        }

        ShowModList();
        RefreshModList();
    }

    private void ShowMessage(string message, bool showButton) {
        if (_mainContainer == null || _messageContainer == null) return;

        _mainContainer.SuspendLayout();
        _mainContainer.Controls.Clear();

        _messageLabel!.Text = message;
        _initializeButton!.Visible = showButton;

        _mainContainer.Controls.Add(_messageContainer, 0, 0);
        _mainContainer.ResumeLayout();
    }

    private void ShowModList() {
        if (_mainContainer == null) return;

        _mainContainer.SuspendLayout();
        _mainContainer.Controls.Clear();

        var container = new TableLayoutPanel {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };

        container.RowStyles.Add(new RowStyle(SizeType.Percent, 70F));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));

        var descriptionContainer = CreateDescriptionContainer();

        container.Controls.Add(_modListView, 0, 0);
        container.Controls.Add(descriptionContainer, 0, 1);

        _mainContainer.Controls.Add(container, 0, 0);
        _mainContainer.ResumeLayout();
    }

    protected sealed override void InitializeComponents() {
        _mainContainer = new TableLayoutPanel {
            Dock = DockStyle.Fill,
            RowCount = 1,
            ColumnCount = 1
        };

        InitializeMessageContainer();
        InitializeModListView();

        Tab.Controls.Add(_mainContainer);
    }

    private void InitializeMessageContainer() {
        _messageContainer = new TableLayoutPanel {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(20)
        };

        _messageContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        _messageContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        _messageLabel = new Label {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomCenter,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 12)
        };

        _initializeButton = new Button {
            Text = "Initialize",
            Width = 120,
            Height = 40,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _initializeButton.Click += InitializeButton_Click;

        var buttonPanel = new Panel {
            Dock = DockStyle.Fill,
            Height = 40
        };
        buttonPanel.Controls.Add(_initializeButton);
        _initializeButton.Location = new Point((buttonPanel.Width - _initializeButton.Width) / 2, 0);

        _messageContainer.Controls.Add(_messageLabel, 0, 0);
        _messageContainer.Controls.Add(buttonPanel, 0, 1);
    }

    private void InitializeModListView() {
        _modListView = new ListView {
            Dock = DockStyle.Fill,
            View = View.Details,
            CheckBoxes = true,
            AllowDrop = true,
            FullRowSelect = true,
            AutoSize = true,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable
        };

        _modListView.Columns.Add("Mod Name", -1);
        _modListView.Columns.Add("Author", -1);
        _modListView.Columns.Add("Version", -1);

        _modListView.ColumnWidthChanging += (_, e) => {
            e.Cancel = true;
            e.NewWidth = _modListView.Columns[e.ColumnIndex].Width;
        };

        _modListView.SizeChanged += (_, _) => {
            var totalWidth = _modListView.ClientSize.Width;
            if (totalWidth <= 0) return;

            _modListView.Columns[0].Width = totalWidth - 250;
            _modListView.Columns[1].Width = 170;
            _modListView.Columns[2].Width = 80;
        };
    }

    private TableLayoutPanel CreateDescriptionContainer() {
        var descriptionContainer = new TableLayoutPanel {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        descriptionContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        descriptionContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var buttonPanel = new FlowLayoutPanel {
            Dock = DockStyle.Fill,
            Height = 45,
            FlowDirection = FlowDirection.LeftToRight
        };

        var refreshButton = new Button {
            Text = "Refresh Available Mods",
            Width = 220,
            Height = 35
        };
        refreshButton.Click += (_, _) => {
            SettingsSaver.PauseSaving();
            var tempInstallPath = SettingsHolder.InstallPath;
            var tempIsPatched = SettingsHolder.IsPatched;
            SettingsHolder.InstallPath = string.Empty;
            SettingsHolder.InstallPath = tempInstallPath;
            SettingsHolder.IsPatched = tempIsPatched;
            SettingsSaver.UnpauseSaving();
        };

        var enableAllButton = new Button {
            Text = "Enable All",
            Width = 150,
            Height = 35
        };
        enableAllButton.Click += (_, _) => SetAllModsChecked(true);

        var disableAllButton = new Button {
            Text = "Disable All",
            Width = 150,
            Height = 35
        };
        disableAllButton.Click += (_, _) => SetAllModsChecked(false);

        var openModsFolderButton = new Button {
            Text = "Open Mods folder",
            Width = 150,
            Height = 35
        };
        openModsFolderButton.Click += (_, _) => OpenModsFolder();

        _descriptionBox = new NoCaretTextBox {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            BackColor = SystemColors.Window,
            Text = "Select a mod to view its description, changes and conflicts.",
            ForeColor = Color.Gray,
            ScrollBars = ScrollBars.Vertical
        };

        buttonPanel.Controls.Add(refreshButton);
        buttonPanel.Controls.Add(disableAllButton);
        buttonPanel.Controls.Add(enableAllButton);
        buttonPanel.Controls.Add(openModsFolderButton);
        descriptionContainer.Controls.Add(buttonPanel, 0, 0);
        descriptionContainer.Controls.Add(_descriptionBox, 0, 1);

        return descriptionContainer;
    }

    private void SetAllModsChecked(bool value) {
        if (_modListView == null || _isRefreshing) return;

        try {
            _isRefreshing = true;

            foreach (ListViewItem item in _modListView.Items) {
                if (item.Tag is not Mod mod) continue;

                item.Checked = value;
                mod.IsEnabled = value;
                RefreshItem(item);
            }

            SettingsHolder.UpdateModState(ModManager.Mods);
            ModsChanged?.Invoke();
        }
        finally {
            _isRefreshing = false;
        }
    }

    private void OpenModsFolder() {
        var mods = Path.Join(SettingsHolder.InstallPath, "Mods");
        Process.Start("explorer.exe", mods);
    }

    private void InitializeButton_Click(object? sender, EventArgs e) {
        var installPath = SettingsHolder.InstallPath;
        if (string.IsNullOrEmpty(installPath)) return;

        var modsPath = Path.Combine(installPath, "Mods");
        var logsPath = Path.Combine(installPath, "Logs");

        Directory.CreateDirectory(modsPath);
        Directory.CreateDirectory(logsPath);

        UpdateUIState();
    }

    private ListViewItem CreateListViewItem(Mod mod) {
        var item = new ListViewItem {
            Text = mod.Info.Name,
            Checked = mod.IsEnabled,
            Tag = mod
        };

        item.SubItems.Add(mod.Info.Author);
        item.SubItems.Add(mod.Info.Version);

        var conflict = ConflictManager.GetConflictDisplay(mod, ModManager.Mods);
        var dependency = DependencyManager.ValidateDependencies(mod, ModManager.Mods);

        item.BackColor = dependency.HasErrors ? dependency.DisplayColor : conflict.BackgroundColor;
        mod.DependencyError = dependency.HasErrors ? dependency.ErrorMessage : string.Empty;

        return item;
    }

    private void ModListView_ItemChecked(object? sender, ItemCheckedEventArgs e) {
        if (_isRefreshing || e.Item?.Tag is not Mod mod || _modListView == null) return;

        // Very dirty workaround to prevent isPatched being set to false during init.
        if (Environment.StackTrace.Contains("CreateControl"))
            return;

        mod.IsEnabled = e.Item.Checked;
        RefreshItem(e.Item);
        SettingsHolder.UpdateModState(ModManager.Mods);
        ModsChanged?.Invoke();
    }

    private void RefreshItem(ListViewItem item) {
        if (_isRefreshing || _modListView == null || item?.Tag is not Mod mod) return;

        try {
            _isRefreshing = true;

            var conflict = ConflictManager.GetConflictDisplay(mod, ModManager.Mods);
            var dependency = DependencyManager.ValidateDependencies(mod, ModManager.Mods);

            item.BackColor = dependency.HasErrors ? dependency.DisplayColor : conflict.BackgroundColor;
            mod.DependencyError = dependency.HasErrors ? dependency.ErrorMessage : string.Empty;

            foreach (ListViewItem otherItem in _modListView.Items) {
                if (otherItem == null || otherItem == item || otherItem.Tag is not Mod otherMod) continue;

                var conflict2 = ConflictManager.GetConflictDisplay(otherMod, ModManager.Mods);
                var dependency2 = DependencyManager.ValidateDependencies(otherMod, ModManager.Mods);

                otherItem.BackColor = dependency2.HasErrors ? dependency2.DisplayColor : conflict2.BackgroundColor;
                otherMod.DependencyError = dependency2.HasErrors ? dependency2.ErrorMessage : string.Empty;
            }
        }
        finally {
            _isRefreshing = false;
        }
    }

    private void RefreshModList() {
        if (_isRefreshing || _modListView == null) return;

        try {
            _isRefreshing = true;
            var currentItems = ModManager.Mods.Select(CreateListViewItem).ToArray();

            _modListView!.BeginUpdate();
            _modListView.Items.Clear();
            _modListView.Items.AddRange(currentItems);
            _modListView.EndUpdate();
        }
        finally {
            _isRefreshing = false;
        }
    }

    private void InitializeEvents() {
        _modListView!.ItemDrag += ModListView_ItemDrag;
        _modListView!.DragOver += ModListView_DragOver;
        _modListView!.DragDrop += ModListView_DragDrop;
        _modListView!.ItemChecked += ModListView_ItemChecked;
        _modListView!.SelectedIndexChanged += ModListView_SelectedIndexChanged;
        _modListView!.MouseClick += ModListView_MouseClick;
    }

    private void ModListView_SelectedIndexChanged(object? sender, EventArgs e) {
        if (_modListView!.SelectedItems.Count == 0) {
            _descriptionBox!.Text = string.Empty;
            _descriptionBox!.ForeColor = Color.Gray;
            return;
        }

        var mod = (Mod)_modListView.SelectedItems[0].Tag;
        var modificationInfo = mod.GetModificationTypes();
        var display = ConflictManager.GetConflictDisplay(mod, ModManager.Mods);

        _descriptionBox!.Text =
            $"{mod.Info.Description}\r\n{modificationInfo}\r\n{display.ToolTip}{mod.DependencyError}";
        _descriptionBox!.ForeColor = SystemColors.WindowText;
    }

    private void ModListView_MouseClick(object? sender, MouseEventArgs e) {
        if (e.Button != MouseButtons.Right) return;

        var focusedItem = _modListView!.FocusedItem;
        if (focusedItem == null) return;

        var mod = focusedItem.Tag as Mod;
        if (string.IsNullOrEmpty(mod!.Info.Url)) return;

        var contextMenu = new ContextMenuStrip();
        var openUrlItem = new ToolStripMenuItem("Open URL");
        openUrlItem.Click += (_, _) => {
            Process.Start(new ProcessStartInfo {
                FileName = mod.Info.Url,
                UseShellExecute = true
            });
        };

        contextMenu.Items.Add(openUrlItem);
        contextMenu.Show(_modListView, e.Location);
    }

    private void ModListView_ItemDrag(object? sender, ItemDragEventArgs e) {
        _modListView!.DoDragDrop(e.Item!, DragDropEffects.Move);
    }

    private void ModListView_DragOver(object? sender, DragEventArgs e) {
        e.Effect = DragDropEffects.Move;
    }

    private void ModListView_DragDrop(object? sender, DragEventArgs e) {
        var targetPoint = _modListView!.PointToClient(new Point(e.X, e.Y));
        var targetItem = _modListView!.GetItemAt(targetPoint.X, targetPoint.Y);
        var draggedItem = (ListViewItem?)e.Data?.GetData(typeof(ListViewItem));

        if (targetItem == null || draggedItem == null)
            return;

        ModManager.UpdateModOrder(draggedItem.Index, targetItem.Index);
        SettingsHolder.UpdateModState(ModManager.Mods);
        ModsChanged?.Invoke();
        RefreshModList();
    }
}