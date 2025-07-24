using P2ModLoader.Forms.Tabs;
using P2ModLoader.Helper;
using P2ModLoader.Logging;
using P2ModLoader.ModList;
using P2ModLoader.Patching;
using P2ModLoader.Update;
using AutoUpdater = P2ModLoader.Update.AutoUpdater;

namespace P2ModLoader.Forms;

public class MainForm : Form {
    private Button? _patchButton;
    private Button? _launchExeButton;
    private Button? _launchSteamButton;
    private ModsTab? _modsTab;
    private Label? _patchStatusLabel;
    private StatusStrip _statusStrip;
    private ToolStripStatusLabel _logStatusLabel;
    private LogViewerForm? _logViewerForm;
    
    public MainForm() { 	
        //InitializeComponent();
        InitializeTabs();
        Load += MainForm_Load!;
    }
    
    private static async void MainForm_Load(object sender, EventArgs e) { 	
        if (SettingsHolder.CheckForUpdatesOnStartup)
            await AutoUpdater.CheckForUpdatesAsync();
    }

    private void InitializeTabs() { 	
        Text = $"P2ModLoader {VersionComparison.CurrentLoaderVersion}";
        Size = SettingsHolder.WindowSize;
        MinimumSize = new Size(600, 700); 

        ResizeEnd += (_, _) => { if (WindowState == FormWindowState.Normal) { SettingsHolder.WindowSize = Size; } };
        
        var mainContainer = new TableLayoutPanel {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));

        var tabControl = new TabControl();
        tabControl.Dock = DockStyle.Fill;

        var modsTabPage = new TabPage("Mods");
        var settingsTabPage = new TabPage("Settings");
        var savesTabPage = new TabPage("Saves");

        _modsTab = new ModsTab(modsTabPage);
        _ = new SettingsTab(settingsTabPage, this);
        _ = new SavesTab(savesTabPage);

        tabControl.TabPages.Add(modsTabPage);
        tabControl.TabPages.Add(settingsTabPage);
        tabControl.TabPages.Add(savesTabPage);

        _patchStatusLabel = new Label {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            Margin = new Padding(5)
        };
        
        var buttonContainer = new TableLayoutPanel {
            Dock = DockStyle.Fill,
            RowCount = 1,
            ColumnCount = 3,
            Margin = new Padding(5)
        };

        buttonContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27F));
        buttonContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
        buttonContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));

        _patchButton = NewButton();
        _patchButton.Click += (_, _) => {
            GamePatcher.TryPatch();
            UpdateControls();
        };

        _launchExeButton = NewButton();
        _launchExeButton.Click += (_, _) => {
            GameLauncher.LaunchExe();
            UpdateControls();
        };

        _launchSteamButton = NewButton();
        _launchSteamButton.Click += (_, _) => {
            GameLauncher.LaunchSteam();
            UpdateControls();
        };

        buttonContainer.RowCount = 2;
        buttonContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
        buttonContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
        buttonContainer.Controls.Add(_patchStatusLabel, 0, 0);
        buttonContainer.SetColumnSpan(_patchStatusLabel, 3);
        buttonContainer.Controls.Add(_patchButton, 0, 1);
        buttonContainer.Controls.Add(_launchExeButton, 1, 1);
        buttonContainer.Controls.Add(_launchSteamButton, 2, 1);

        mainContainer.Controls.Add(tabControl, 0, 0);
        mainContainer.Controls.Add(buttonContainer, 0, 1);

        Controls.Add(mainContainer);

        SettingsHolder.PatchStatusChanged += UpdateControls;
        SettingsHolder.InstallPathChanged += UpdateControls;
        SettingsHolder.StartupWithConflictsChanged += UpdateControls;
        _modsTab.ModsChanged += UpdateControls;
        UpdateControls();
        
        _statusStrip = new StatusStrip { 
            Height = 25,
            Dock = DockStyle.Bottom 
        };

        _logStatusLabel = new ToolStripStatusLabel { 
            Spring = true, 
            TextAlign = ContentAlignment.MiddleLeft 
        };
        _logStatusLabel.Text = "Test message - status strip is working!";
        _logStatusLabel.Click += OnLogStatusClick;
        _statusStrip.Items.Add(_logStatusLabel);;
        Controls.Add(_statusStrip);

        Logger.LogMessageAdded += OnLogMessageAdded;
    }
    
    protected override void Dispose(bool disposing) {
        if (disposing) 
            Logger.LogMessageAdded -= OnLogMessageAdded;
        base.Dispose(disposing);
    }
    
    private void OnLogMessageAdded(string message) {
        if (InvokeRequired) {
            Invoke(() => OnLogMessageAdded(message));
            return;
        }

        var displayMessage = message;
        if (message.Contains("] ")) 
            displayMessage = message[(message.IndexOf("] ", StringComparison.Ordinal) + 2)..];

        _logStatusLabel.Text = displayMessage.Length > 100 ? $"{displayMessage[..97]}..." : displayMessage;
    }

    private void OnLogStatusClick(object? sender, EventArgs e) {
        if (_logViewerForm == null || _logViewerForm.IsDisposed) 
            _logViewerForm = new LogViewerForm();

        _logViewerForm.Show();
    }

    private static Button NewButton() => new() {
        Dock = DockStyle.Fill,
        Height = 40,
        Margin = new Padding(5)
    };
    
    public void UpdateControls() { 	
        var hasConflicts = _modsTab!.HasFileConflicts() || DependencyManager.HasDependencyErrors(ModManager.Mods);
        var shouldDisableButtons = SettingsHolder.InstallPath == null ||
                                   (!SettingsHolder.AllowStartupWithConflicts && hasConflicts) ||
                                   ModManager.Mods.Count == 0;
        var hasMods = ModManager.Mods.Any(m => m.IsEnabled);
            
        _patchButton!.Enabled = !shouldDisableButtons && !SettingsHolder.IsPatched;
        _launchExeButton!.Enabled = !shouldDisableButtons;
        _launchSteamButton!.Enabled = !shouldDisableButtons && InstallationLocator.FindSteam() != null;
        _patchButton.Text = hasMods ? "Patch" : "Restore Default";
        _launchExeButton.Text = SettingsHolder.IsPatched ? "Launch.exe" : "Patch + Launch .exe";
        _launchSteamButton.Text = SettingsHolder.IsPatched ? "Launch in Steam" : "Patch + Launch in Steam";
        
        if (_patchStatusLabel?.InvokeRequired == true) {
            _patchStatusLabel.Invoke(UpdateControls);
            return;
        }

        var unpatchedText = hasMods ? "Current mod list might not been applied yet. Patch to apply changes."
                : "There may still be applied mods. Restore default to recover backups.";
        var patchedText = hasMods ? "Current mod list has been applied to the game." :
            "No mods have been applied to the game.";
        
        _patchStatusLabel!.Text = 
            SettingsHolder.InstallPath == null ? "The install has not been found, patching unavailable." :
            hasConflicts ? "Resolve conflicts in the mod list before patching." :
            !SettingsHolder.IsPatched ? unpatchedText : patchedText;
            
        _patchStatusLabel.ForeColor = !SettingsHolder.IsPatched ? Color.Red : Color.Black;
    }
}
