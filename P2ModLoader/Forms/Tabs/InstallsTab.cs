using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using P2ModLoader.Abstract;
using P2ModLoader.Data;
using P2ModLoader.Helper;
using P2ModLoader.Logging;
using P2ModLoader.ModList;

namespace P2ModLoader.Forms.Tabs;

public class InstallsTab : BaseTab {
    private ListView? _installsListView;
    private Button? _locateButton, _browseButton;
    private Button? _removeButton, _editLabelButton,  _openFolderButton, _openGameLogButton;
    private ImageList? _imageList;
    private Image? _steamIcon;

    private Install? SelectedInstall => _installsListView?.SelectedItems.Count > 0 
        ? (Install)_installsListView.SelectedItems[0].Tag! : null;

    public InstallsTab(TabPage page) : base(page) {
        InitializeComponents();
        LoadInstalls();

        SettingsHolder.InstallsChanged += LoadInstalls;
        SettingsHolder.InstallPathChanged += () => _installsListView?.Refresh();
    }

    protected sealed override void InitializeComponents() {
        _steamIcon = ResourcesLoader.LoadImage("steam", "png");

        var mainContainer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        _installsListView = new ListView {
            Dock = DockStyle.Fill,
            View = View.Details,
            SmallImageList = _imageList = new ImageList { ImageSize = new Size(32, 32) },
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.None,
            ShowItemToolTips = true,
            OwnerDraw = true
        };

        _installsListView.Columns.Add(string.Empty, -2);

        _installsListView.SelectedIndexChanged += (_, _) => {
            var hasSelection = SelectedInstall != null;
            _editLabelButton!.Enabled = hasSelection;
            _removeButton!.Enabled = hasSelection;
            _openFolderButton!.Enabled = hasSelection;
            _openGameLogButton!.Enabled = hasSelection && SelectedInstall!.GameAppDataName != null;
        };
        _installsListView.MouseClick += InstallsListView_MouseClick;
        _installsListView.MouseDoubleClick += (_, e) => {
            if (_installsListView!.GetItemAt(e.X, e.Y)?.Tag is Install install)
                OpenFolder(install);
        };
        _installsListView.DrawSubItem += InstallsListView_DrawSubItem;
        _installsListView.KeyDown += InstallsListView_KeyDown;
        _installsListView.SizeChanged += (_, _) => {
            _installsListView.BeginUpdate();
            _installsListView.Columns[0].Width = -2;
            _installsListView.EndUpdate();
        };

        mainContainer.Controls.Add(CreateTopPanel(), 0, 0);
        mainContainer.Controls.Add(_installsListView, 0, 1);
        mainContainer.Controls.Add(CreateBottomPanel(), 0, 2);

        Tab.Controls.Add(mainContainer);
    }

    private TableLayoutPanel CreateTopPanel() {
        var panel = new TableLayoutPanel {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));

        var leftFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        _browseButton = CreateButton("Browse...", 100, BrowseButton_Click);
        leftFlow.Controls.Add(_browseButton);

        var rightFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        _locateButton = CreateButton("Locate Steam installs automatically", 340, LocateButton_Click);
        rightFlow.Controls.Add(_locateButton);

        panel.Controls.Add(leftFlow, 0, 0);
        panel.Controls.Add(rightFlow, 1, 0);

        return panel;
    }

    private Panel CreateBottomPanel() {
        var panel = new FlowLayoutPanel {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0)
        };

        _editLabelButton = CreateButton("Edit Label", 100, (_, _) => EditLabel(SelectedInstall!), false);
        _removeButton = CreateButton("Remove", 90, (_, _) => RemoveInstall(SelectedInstall!), false);
        _openFolderButton = CreateButton("Open Folder", 125, (_, _) => OpenFolder(SelectedInstall!), false);
        _openGameLogButton = CreateButton("Open Game Log", 150, (_, _) => OpenGameLog(SelectedInstall!), false);

        panel.Controls.AddRange([_editLabelButton, _removeButton, _openFolderButton, _openGameLogButton]);
        return panel;
    }

    private static Button CreateButton(string text, int width, EventHandler onClick, bool enabled = true) {
        var button = new Button {
            Text = text,
            Width = width,
            Height = 35,
            Enabled = enabled
        };
        button.Click += onClick;
        return button;
    }
    
    [SuppressMessage("ReSharper", "SwitchStatementMissingSomeEnumCasesNoDefault")]
    private void InstallsListView_KeyDown(object? sender, KeyEventArgs e) {
        if (SelectedInstall == null) return;

        switch (e.KeyCode) {
            case Keys.Enter:
                OpenFolder(SelectedInstall);
                e.Handled = true;
                break;
            case Keys.Delete:
                RemoveInstall(SelectedInstall);
                e.Handled = true;
                break;
        }
    }

    private void InstallsListView_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e) {
        if (e.Item!.Tag is not Install install) return;

        e.Graphics.FillRectangle(e.Item.Selected ? SystemBrushes.Highlight : new SolidBrush(e.Item.BackColor), e.Bounds);

        var image = _imageList?.Images[e.Item.ImageKey];
        e.Graphics.DrawImage(image!, new Rectangle(e.Bounds.Left + 2, e.Bounds.Top + 2, 32, 32));

        var isActive = SettingsHolder.SelectedInstall?.Id == install.Id;
        var font = isActive ? new Font(e.Item.Font!, FontStyle.Bold) : e.Item.Font;
        var textColor = e.Item.Selected ? SystemColors.HighlightText : e.Item.ForeColor;
        var textRect = new Rectangle(e.Bounds.Left + 40, e.Bounds.Top, e.Bounds.Width - 80, e.Bounds.Height);
        const TextFormatFlags textFormatFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left;
        
        TextRenderer.DrawText(e.Graphics, e.Item.Text, font, textRect, textColor, textFormatFlags);

        if (install.IsSteamInstall && _steamIcon != null)
            e.Graphics.DrawImage(_steamIcon, new Rectangle(e.Bounds.Right - 34, e.Bounds.Top, 30, 30));
    }

    private void LoadInstalls() {
        if (_installsListView == null || _imageList == null) return;

        _installsListView.Items.Clear();
        _imageList.Images.Clear();

        foreach (var install in SettingsHolder.Installs) {
            _imageList.Images.Add(install.Id, ResourcesLoader.LoadImage(install.DisplayImage)!);

            var item = new ListViewItem(install.DisplayName) {
                Tag = install,
                ImageKey = install.Id,
                ToolTipText = install.InstallPath
            };

            _installsListView.Items.Add(item);
        }

        _installsListView.Refresh();
    }

    private void InstallsListView_MouseClick(object? sender, MouseEventArgs e) {
        if (e.Button != MouseButtons.Right || _installsListView!.FocusedItem?.Tag is not Install install) return;

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(CreateMenuItem("Edit Label", (_, _) => EditLabel(install)));
        contextMenu.Items.Add(CreateMenuItem("Remove", (_, _) => RemoveInstall(install)));
        contextMenu.Items.Add(CreateMenuItem("Open Folder", (_, _) => OpenFolder(install)));
        contextMenu.Items.Add(CreateMenuItem("Open Game Log", (_, _) => 
            OpenGameLog(install), install.GameAppDataName != null));
        
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(CreateMenuItem("Copy Path", (_, _) => Clipboard.SetText(install.InstallPath)));

        contextMenu.Show(_installsListView, e.Location);
    }

    private static ToolStripMenuItem CreateMenuItem(string text, EventHandler onClick, bool enabled = true) {
        var item = new ToolStripMenuItem(text);
        item.Click += onClick;
        item.Enabled = enabled;
        return item;
    }

    private static void OpenFolder(Install install) => Process.Start("explorer.exe", install.InstallPath);

    private static void OpenGameLog(Install install) {
        var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
        
        var logPath = Path.Combine(userProfile!, "AppData/LocalLow/Ice-Pick Lodge",
            install.GameAppDataName!, install.PlayerLogName!);

        Logger.Log(LogLevel.Info, $"Opening log for {install.Game} at {logPath}.");
        if (!File.Exists(logPath)) {
            MessageBox.Show($"Game log file not found at:\n{logPath}\n\nThe game may not have been run yet.",
                "Log Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try {
            Process.Start("explorer.exe", $"/select,\"{logPath}\"");
        } catch (Exception ex) {
            MessageBox.Show($"Failed to open game log:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LocateButton_Click(object? sender, EventArgs e) {
        var existingInstalls = SettingsHolder.Installs.ToList();
        var newInstalls = InstallationLocator.LocateAllInstalls(existingInstalls);

        if (newInstalls.Count > 0) {
            foreach (var install in newInstalls) {
                ModManager.ScanModsForInstall(install);
                ConflictManager.PrecomputeConflictsForInstall(install);
                SettingsHolder.AddInstall(install);
            }

            MessageBox.Show($"Found and added {newInstalls.Count} new install(s).", "Installs Located",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        } else {
            MessageBox.Show("No new installs found - you can always browse for missed installations manually.",
                "No Installs Located", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void BrowseButton_Click(object? sender, EventArgs e) {
        using var folderDialog = new FolderBrowserDialog { Description = "Select installation folder" };

        if (folderDialog.ShowDialog() != DialogResult.OK) return;

        var gameType = InstallationLocator.DetectGameType(folderDialog.SelectedPath);
        if (gameType == null) {
            MessageBox.Show("Could not detect a valid game installation in the selected folder.",
                "Invalid Install", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var normalizedPath = Path.GetFullPath(folderDialog.SelectedPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (SettingsHolder.Installs.Any(i => {
                var existingPath = Path.GetFullPath(i.InstallPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return existingPath.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase);
            })) {
            MessageBox.Show("This install is already in the list.", "Duplicate Install",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var isSteam = InstallationLocator.IsSteamInstall(folderDialog.SelectedPath);
        var install = new Install(folderDialog.SelectedPath, gameType.Value) {
            CustomLabel = isSteam ? "Steam" : string.Empty,
            IsSteamInstall = isSteam
        };

        SettingsHolder.AddInstall(install);
        ModManager.ScanModsForInstall(install);
        ConflictManager.PrecomputeConflictsForInstall(install);

        MessageBox.Show($"Added: {install.DisplayName}", "Install Added",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void RemoveInstall(Install install) {
        var result = MessageBox.Show(
            $"Remove {install.DisplayName} from the list?\n\nThis will not delete the game itself, or any associated " +
            $"mods, logs or backups and will only remove the installation from the game list. You can always add it " +
            $"back later.", "Confirm Removal", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
            SettingsHolder.RemoveInstall(install.Id);
    }

    private void EditLabel(Install install) {
        using var input = new Form {
            Text = "Edit Label",
            Width = 350,
            Height = 200,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var textBox = new TextBox {
            Text = install.CustomLabel,
            Location = new Point(10, 45),
            Width = 310
        };

        var okButton = new Button {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(135, 90),
            Width = 80,
            Height = 35
        };

        input.Controls.AddRange([
            new Label { Text = "Custom Label:", Location = new Point(10, 20), AutoSize = true },
            textBox,
            okButton,
            new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(225, 90), Width = 80, Height = 35 }
        ]);

        input.AcceptButton = okButton;
        input.CancelButton = input.Controls[3] as Button;

        if (input.ShowDialog() == DialogResult.OK) {
            install.CustomLabel = textBox.Text;
            SettingsHolder.TriggerInstallsChanged();
        }
    }
}