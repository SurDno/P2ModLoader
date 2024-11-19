using System.Reflection;
using P2ModLoader.Forms.Tabs;
using P2ModLoader.Helper;
using P2ModLoader.ModList;

namespace P2ModLoader.Forms;

public partial class MainForm : Form {
    private Button? _patchButton;
    private Button? _launchExeButton;
    private Button? _launchSteamButton;
    private ModsTab? _modsTab;
    private Label? _patchStatusLabel;
    
    public MainForm() {
        InitializeComponent();
        InitializeTabs();
        this.Load += MainForm_Load!;
    }
    
    private static async void MainForm_Load(object sender, EventArgs e) {
        if (SettingsHolder.CheckForUpdates)
            await AutoUpdater.CheckForUpdatesAsync();
    }

    private void InitializeTabs() {
        var versionInfo = Assembly.GetExecutingAssembly().GetName().Version!;
        var version = $"{versionInfo.Major}.{versionInfo.Minor}.{versionInfo.Build}";
        Text = $"P2ModLoader {version}";
        Size = new Size(800, 800);
        MinimumSize = new Size(600, 600); 

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

        buttonContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        buttonContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        buttonContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));

        _patchButton = NewButton("Patch");
        _patchButton.Click += (_, _) => {
            GameLauncher.TryPatch(setIsPatchedEarly: true);
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
    }

    private static Button NewButton(string text = "") => new() {
        Text = text,
        Dock = DockStyle.Fill,
        Height = 40,
        Margin = new Padding(5)
    };
    
    public void UpdateControls() {
        var hasConflicts = _modsTab.HasFileConflicts() || DependencyManager.HasDependencyErrors(ModManager.Mods);
        var shouldDisableButtons = SettingsHolder.InstallPath == null ||
                                   (!SettingsHolder.AllowStartupWithConflicts && hasConflicts) ||
                                   ModManager.Mods.Count == 0;

        _patchButton!.Enabled = !shouldDisableButtons && !SettingsHolder.IsPatched;
        _launchExeButton!.Enabled = !shouldDisableButtons;
        _launchSteamButton!.Enabled = !shouldDisableButtons;
        _launchExeButton.Text = SettingsHolder.IsPatched ? "Launch.exe" : "Patch + Launch .exe";
        _launchSteamButton.Text = SettingsHolder.IsPatched ? "Launch in Steam" : "Patch + Launch in Steam";
        
        if (_patchStatusLabel?.InvokeRequired == true) {
            _patchStatusLabel.Invoke(UpdateControls);
            return;
        }
    
        _patchStatusLabel!.Text = 
            SettingsHolder.InstallPath == null ? "The install has not been found, patching unavailable." :
            hasConflicts ? "Resolve conflicts in the mod list before patching." :
            !SettingsHolder.IsPatched ? "Current mod list has not been applied yet. Patch to apply the changes." :
            "Current mod list has been applied to the game.";
        _patchStatusLabel.ForeColor = !SettingsHolder.IsPatched ? Color.Red : Color.Black;
    }
}
