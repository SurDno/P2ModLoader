using System.Diagnostics;
using P2ModLoader.Abstract;
using P2ModLoader.Helper;
using P2ModLoader.Logging;

namespace P2ModLoader.Forms.Tabs;

public class SettingsTab : BaseTab {
    private readonly MainForm? _mainForm;

    public SettingsTab(TabPage page, MainForm mainForm) : base(page) {
        using var perf = PerformanceLogger.Log();
        _mainForm = mainForm;
        InitializeComponents();
    }
    
    protected sealed override void InitializeComponents() {
        using var perf = PerformanceLogger.Log();
        var logButtonsPanel = new Panel();
        logButtonsPanel.Width = 500;
        logButtonsPanel.Height = 40;
        logButtonsPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        
        var p2LogButton = new Button();
        p2LogButton.Text = "Open Pathologic 2 log";
        p2LogButton.Width = 210;
        p2LogButton.Height = 32;
        p2LogButton.Dock = DockStyle.Right;
        p2LogButton.Click += (_, _) => Process.Start("explorer.exe", InstallationLocator.FindLogFile());

        var modLoaderLogButton = new Button();
        modLoaderLogButton.Text = "Open P2ModLoader log";
        modLoaderLogButton.Width = 240;
        modLoaderLogButton.Height = 32;
        modLoaderLogButton.Dock = DockStyle.Right;
        modLoaderLogButton.Click += (_, _) => Process.Start("explorer.exe", Logger.GetLogPath());

        logButtonsPanel.Controls.AddRange([modLoaderLogButton, p2LogButton]);
        
        var pathLabel = new Label();
        pathLabel.Text = "Installation Path:";
        pathLabel.Location = new Point(20, 20);
        pathLabel.AutoSize = true;

        var pathTextBox = new TextBox();
        pathTextBox.Location = new Point(20, 45);
        pathTextBox.Width = 400;
        pathTextBox.Height = 28;

        pathTextBox.Text = SettingsHolder.InstallPath ?? string.Empty;
        SettingsHolder.InstallPathChanged += () => {
            pathTextBox.Text = SettingsHolder.InstallPath ?? string.Empty;
        };
        
        var browseButton = new Button();
        browseButton.Text = "Browse";
        browseButton.Location = new Point(430, 45);
        browseButton.Width = 80;
        browseButton.Height = 32;
        browseButton.Click += (_, _) => {
            using var folderDialog = new FolderBrowserDialog();
            if (folderDialog.ShowDialog() != DialogResult.OK) return;
            SettingsHolder.InstallPath = folderDialog.SelectedPath;
            _mainForm!.UpdateControls();
        };

        var locateButton = new Button();
        locateButton.Text = "Locate";
        locateButton.Location = new Point(520, 45);
        locateButton.Width = 80;
        locateButton.Height = 32;
        locateButton.Click += (_, _) => {
            var installPath = InstallationLocator.FindInstall();
            if (string.IsNullOrEmpty(installPath)) return;
            SettingsHolder.InstallPath = installPath;
            _mainForm!.UpdateControls();
        };

        var allowConflictsCheckBox = new CheckBox();
        allowConflictsCheckBox.Text = "Allow startup with conflicts (not recommended)";
        allowConflictsCheckBox.Location = new Point(20, 85);
        allowConflictsCheckBox.AutoSize = true;
        allowConflictsCheckBox.Checked = SettingsHolder.AllowStartupWithConflicts;
        allowConflictsCheckBox.CheckedChanged += (_, _) => {
            SettingsHolder.AllowStartupWithConflicts = allowConflictsCheckBox.Checked;
        };

        var checkForUpdatesButton = new Button();
        checkForUpdatesButton.Text = "Check for updates";
        checkForUpdatesButton.Location = new Point(20, 125);
        checkForUpdatesButton.Width = 190;
        checkForUpdatesButton.Height = 32;
        checkForUpdatesButton.Click += (_, _) => _ = AutoUpdater.CheckForUpdatesAsync(showNoUpdatesDialog: true); 
        
        var checkForUpdatesCheckBox = new CheckBox();
        checkForUpdatesCheckBox.Text = "Check for updates on startup";
        checkForUpdatesCheckBox.Location = new Point(20, 165);
        checkForUpdatesCheckBox.AutoSize = true;
        checkForUpdatesCheckBox.Checked = SettingsHolder.CheckForUpdatesOnStartup;
        checkForUpdatesCheckBox.CheckedChanged += (_, _) => {
            SettingsHolder.CheckForUpdatesOnStartup = checkForUpdatesCheckBox.Checked;
        };

        logButtonsPanel.Location = new Point(Tab.Width - logButtonsPanel.Width - 20, Tab.Height - logButtonsPanel.Height - 20);
        
        Tab.Controls.AddRange([
            pathLabel,
            pathTextBox,
            browseButton,
            locateButton,
            allowConflictsCheckBox,
            checkForUpdatesButton,
            checkForUpdatesCheckBox,
            logButtonsPanel
        ]);
    }
}