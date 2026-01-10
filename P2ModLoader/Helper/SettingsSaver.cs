using System.Text.Json;
using P2ModLoader.Data;
using P2ModLoader.Logging;

namespace P2ModLoader.Helper;

public static class SettingsSaver {
    private static readonly string SettingsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings");
    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private static bool _subscribed;
    private static bool _pauseSaving;
    
    private class SavedSettings {
        public List<SavedInstall> Installs { get; init; } = [];
        public string? SelectedInstallId { get; init; }
        public bool AllowStartupWithConflicts { get; init; }
        public bool CheckForUpdates { get; init; }
        public Size WindowSize { get; init; }
        public LogLevel LogLevel { get; init; } = LogLevel.Info;
    }
    
    public static void PauseSaving() => _pauseSaving = true; 
    public static void UnpauseSaving() => _pauseSaving = false; 
    
    public static void LoadSettings() { 	
        if (File.Exists(SettingsPath)) {
            try {
                var jsonContent = File.ReadAllText(SettingsPath);
            
                if (jsonContent.Contains("\"InstallPath\"")) {
                    MessageBox.Show("P2ModLoader now supports Pathologic 3 and its demos!\n\n" +
                                    "You will now need to specify the path to your previous Pathologic 2 " +
                                    "installation again, which can be done in \"Installs\" tab.",
                                    "Old Install Format Detected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                
                var settings = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(SettingsPath));

                if (settings == null) {
                    Logger.Log(LogLevel.Info, $"No settings.json file has been found, default settings will be used");
                    return;
                }

                var installs = settings.Installs.Select(si => {
                    if (!Enum.TryParse<Game>(si.Game, out _)) { }

                    return new Install(si.Id, si.InstallPath, Enum.Parse<Game>(si.Game), si.CustomLabel,
                        si.IsSteamInstall, si.IsPatched, si.ModState);
                }).ToList();
                
                SettingsHolder.Installs = installs;
                SettingsHolder.SelectInstall(settings.SelectedInstallId);
                
                SettingsHolder.AllowStartupWithConflicts = settings.AllowStartupWithConflicts;
                SettingsHolder.CheckForUpdatesOnStartup = settings.CheckForUpdates;
                SettingsHolder.WindowSize = settings.WindowSize;
                SettingsHolder.LogLevel = settings.LogLevel;
                Logger.Log(LogLevel.Info, $"Applied settings from settings.json");
            } catch (Exception ex) {
                ErrorHandler.Handle("Failed to load settings", ex);
            }
        }

        if (_subscribed) return;
        SettingsHolder.InstallPathChanged += SaveSettings;
        SettingsHolder.InstallsChanged += SaveSettings;
        SettingsHolder.StartupWithConflictsChanged += SaveSettings;
        SettingsHolder.CheckForUpdatesOnStartupChanged += SaveSettings;
        SettingsHolder.PatchStatusChanged += SaveSettings;
        SettingsHolder.ModStateChanged += SaveSettings;
        SettingsHolder.WindowSizeChanged += SaveSettings;
        SettingsHolder.LogLevelChanged += SaveSettings;

        _subscribed = true;
    }
    
    private static void SaveSettings() { 	
        if (_pauseSaving) return;
        
        Logger.Log(LogLevel.Debug, $"Saving new settings to settings.json");
        try {
            Directory.CreateDirectory(SettingsDirectory);
            
            var savedInstalls = SettingsHolder.Installs.Select(i => new SavedInstall {
                Id = i.Id,
                InstallPath = i.InstallPath,
                Game = i.Game.ToString(),
                CustomLabel = i.CustomLabel,
                IsSteamInstall = i.IsSteamInstall,
                IsPatched = i.IsPatched, 
                ModState = i.ModState   
            }).ToList();
            
            var settings = new SavedSettings {
                Installs = savedInstalls,
                SelectedInstallId = SettingsHolder.SelectedInstall?.Id,
                AllowStartupWithConflicts = SettingsHolder.AllowStartupWithConflicts,
                CheckForUpdates = SettingsHolder.CheckForUpdatesOnStartup,
                WindowSize = SettingsHolder.WindowSize,
                LogLevel = SettingsHolder.LogLevel
            };
            
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, Options));
        } catch (Exception ex) {
            ErrorHandler.Handle("Failed to save settings", ex);
        }
    }
}