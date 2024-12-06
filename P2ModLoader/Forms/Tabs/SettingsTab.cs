using P2ModLoader.Abstract;
using P2ModLoader.Helper;

namespace P2ModLoader.Forms.Tabs;

public class SettingsTab : BaseTab {
    private TextBox? _pathTextBox;
    private Button? _browseButton, _locateButton, _checkForUpdatesButton;
    private CheckBox? _allowConflictsCheckBox, _checkForUpdatesCheckBox;
    private MainForm? _mainForm;

    public SettingsTab(TabPage page, MainForm mainForm) : base(page) {
        _mainForm = mainForm;
        InitializeComponents();
    }
    
    protected sealed override void InitializeComponents() {
        var pathLabel = new Label();
        pathLabel.Text = "Installation Path:";
        pathLabel.Location = new Point(20, 20);
        pathLabel.AutoSize = true;

        _pathTextBox = new TextBox();
        _pathTextBox.Location = new Point(20, 45);
        _pathTextBox.Width = 400;
        _pathTextBox.Height = 28;

        _pathTextBox!.Text = SettingsHolder.InstallPath ?? string.Empty;
        SettingsHolder.InstallPathChanged += () => {
            _pathTextBox!.Text = SettingsHolder.InstallPath ?? string.Empty;
        };
        
        _browseButton = new Button();
        _browseButton.Text = "Browse";
        _browseButton.Location = new Point(430, 45);
        _browseButton.Width = 80;
        _browseButton.Height = 32;
        _browseButton.Click += BrowseButton_Click;

        _locateButton = new Button();
        _locateButton.Text = "Locate";
        _locateButton.Location = new Point(520, 45);
        _locateButton.Width = 80;
        _locateButton.Height = 32;
        _locateButton.Click += LocateButton_Click;

        _allowConflictsCheckBox = new CheckBox();
        _allowConflictsCheckBox.Text = "Allow startup with conflicts (not recommended)";
        _allowConflictsCheckBox.Location = new Point(20, 85);
        _allowConflictsCheckBox.AutoSize = true;
        _allowConflictsCheckBox.Checked = SettingsHolder.AllowStartupWithConflicts;
        _allowConflictsCheckBox.CheckedChanged += (_, _) => {
            SettingsHolder.AllowStartupWithConflicts = _allowConflictsCheckBox!.Checked;
        };

        _checkForUpdatesButton = new Button();
        _checkForUpdatesButton.Text = "Check for updates";
        _checkForUpdatesButton.Location = new Point(20, 125);
        _checkForUpdatesButton.Width = 190;
        _checkForUpdatesButton.Height = 32;
        _checkForUpdatesButton.Click += (_, _) => _ = AutoUpdater.CheckForUpdatesAsync(showNoUpdatesDialog: true); 
        
        _checkForUpdatesCheckBox = new CheckBox();
        _checkForUpdatesCheckBox.Text = "Check for updates on startup";
        _checkForUpdatesCheckBox.Location = new Point(20, 165);
        _checkForUpdatesCheckBox.AutoSize = true;
        _checkForUpdatesCheckBox.Checked = SettingsHolder.CheckForUpdatesOnStartup;
        _checkForUpdatesCheckBox.CheckedChanged += (_, _) => {
            SettingsHolder.CheckForUpdatesOnStartup = _checkForUpdatesCheckBox!.Checked;
        };

        Tab.Controls.AddRange([
            pathLabel,
            _pathTextBox,
            _browseButton,
            _locateButton,
            _allowConflictsCheckBox,
            _checkForUpdatesButton,
            _checkForUpdatesCheckBox
        ]);
    }

    private void BrowseButton_Click(object? sender, EventArgs e) {
        using var folderDialog = new FolderBrowserDialog();
        folderDialog.Description = "Select the location of Pathologic.exe file";
        if (folderDialog.ShowDialog() != DialogResult.OK) return;
        SettingsHolder.InstallPath = folderDialog.SelectedPath;
        _mainForm.UpdateControls();
    }

    private void LocateButton_Click(object? sender, EventArgs e) {
        var installPath = InstallationLocator.FindInstall();
        if (string.IsNullOrEmpty(installPath)) return;
        SettingsHolder.InstallPath = installPath;
        _mainForm.UpdateControls();
    }
}