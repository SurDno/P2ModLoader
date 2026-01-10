using System.Diagnostics;
using P2ModLoader.Abstract;
using P2ModLoader.Helper;
using P2ModLoader.Logging;
using P2ModLoader.Update;

namespace P2ModLoader.Forms.Tabs;

public class SettingsTab : BaseTab {
    private readonly MainForm? _mainForm;

    public SettingsTab(TabPage page, MainForm mainForm) : base(page) { 	
        _mainForm = mainForm;
        InitializeComponents();
    }
    
    protected sealed override void InitializeComponents() { 	
        var logButtonsPanel = new Panel {
            Width = 500,
            Height = 40,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        
        var modLoaderLogButton = new Button {
            Text = "Open P2ModLoader log",
            Width = 240,
            Height = 32,
            Dock = DockStyle.Right
        };
        modLoaderLogButton.Click += (_, _) => Process.Start("explorer.exe", Logger.GetLogPath());

        logButtonsPanel.Controls.AddRange([modLoaderLogButton]);
        
        var allowConflictsCheckBox = new CheckBox {
            Text = "Allow startup with conflicts (not recommended)",
            Location = new Point(20, 20),
            AutoSize = true,
            Checked = SettingsHolder.AllowStartupWithConflicts
        };
        allowConflictsCheckBox.CheckedChanged += (_, _) => {
            SettingsHolder.AllowStartupWithConflicts = allowConflictsCheckBox.Checked;
        };

        var checkForUpdatesButton = new Button {
            Text = "Check for updates",
            Location = new Point(20, 60),
            Width = 190,
            Height = 32
        };
        checkForUpdatesButton.Click += (_, _) => _ = AutoUpdater.CheckForUpdatesAsync(showNoUpdatesDialog: true); 
        
        var checkForUpdatesCheckBox = new CheckBox {
            Text = "Check for updates on startup",
            Location = new Point(20, 100),
            AutoSize = true,
            Checked = SettingsHolder.CheckForUpdatesOnStartup
        };
        checkForUpdatesCheckBox.CheckedChanged += (_, _) => {
            SettingsHolder.CheckForUpdatesOnStartup = checkForUpdatesCheckBox.Checked;
        };
        
        var logLevelLabel = new Label {
            Text = "Log Level:",
            Location = new Point(20, 140),
            AutoSize = true
        };

        var logLevelComboBox = new ComboBox {
            Location = new Point(20, 165),
            Width = 200,
            Height = 28,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        var logLevels = Enum.GetValues<LogLevel>().ToArray();
        foreach (var level in logLevels) {
            logLevelComboBox.Items.Add(new { Text = level.ToString(), Value = level });
        }
        logLevelComboBox.DisplayMember = "Text";
        logLevelComboBox.ValueMember = "Value";

        logLevelComboBox.SelectedIndex = Array.IndexOf(logLevels, SettingsHolder.LogLevel);

        logLevelComboBox.SelectedIndexChanged += (_, _) => {
            if (logLevelComboBox.SelectedItem != null) {
                var selectedItem = (dynamic)logLevelComboBox.SelectedItem;
                SettingsHolder.LogLevel = (LogLevel)selectedItem.Value;
            }
        };

        logButtonsPanel.Location = new Point(Tab.Width - logButtonsPanel.Width - 20, 
            Tab.Height - logButtonsPanel.Height - 20);
        
        Tab.Controls.AddRange([
            allowConflictsCheckBox,
            checkForUpdatesButton,
            checkForUpdatesCheckBox,
            logLevelLabel,      
            logLevelComboBox,    
            logButtonsPanel
        ]);
    }
}