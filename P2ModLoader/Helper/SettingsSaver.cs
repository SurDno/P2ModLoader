using P2ModLoader.Data;using System.Text.Json;

namespace P2ModLoader.Helper;

public static class SettingsSaver {
    private static readonly string SettingsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SETTINGS");
    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private static bool _subscribed;
    private static bool _pauseSaving;
    
    private class SavedSettings {
        public string? InstallPath { get; init; }
        public bool AllowStartupWithConflicts { get; init; }
        public bool IsPatched { get; init; }
        public bool CheckForUpdates {get; init; }
        public List<SavedModState> ModState { get; init; } = [];
        public Size WindowSize { get; init; }
    }
    
    public static void PauseSaving() => _pauseSaving = true; 
    public static void UnpauseSaving() => _pauseSaving = false; 
    
    public static void LoadSettings() {
        if (File.Exists(SettingsPath)) {
            try {
                var settings = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(SettingsPath));

                if (settings == null) {
                    Logger.LogInfo("No settings.json file has been found, default settings will be used.");
                    return;
                }

                SettingsHolder.InstallPath = settings.InstallPath == "null" ? null : settings.InstallPath;
                SettingsHolder.AllowStartupWithConflicts = settings.AllowStartupWithConflicts;
                SettingsHolder.IsPatched = settings.IsPatched;
                SettingsHolder.CheckForUpdatesOnStartup = settings.CheckForUpdates;
                SettingsHolder.LastKnownModState = settings.ModState;
                SettingsHolder.WindowSize = settings.WindowSize;
                Logger.LogInfo("Applied settings from settings.json.");
            } catch (Exception ex) {
                ErrorHandler.Handle("Failed to load settings", ex);
            }
        }

        if (_subscribed) return;
        SettingsHolder.InstallPathChanged += SaveSettings;
        SettingsHolder.StartupWithConflictsChanged += SaveSettings;
        SettingsHolder.CheckForUpdatesOnStartupChanged += SaveSettings;
        SettingsHolder.PatchStatusChanged += SaveSettings;
        SettingsHolder.ModStateChanged += SaveSettings;
        SettingsHolder.WindowSizeChanged += SaveSettings;
        _subscribed = true;
    }
    
    private static void SaveSettings() {
        if (_pauseSaving) return;
        
        Logger.LogInfo($"Saving new settings to settings.json.");
        try {
            Directory.CreateDirectory(SettingsDirectory);
            
            var settings = new SavedSettings {
                InstallPath = SettingsHolder.InstallPath == null ? "null" : SettingsHolder.InstallPath,
                AllowStartupWithConflicts = SettingsHolder.AllowStartupWithConflicts,
                IsPatched = SettingsHolder.IsPatched,
                CheckForUpdates = SettingsHolder.CheckForUpdatesOnStartup,
                ModState = SettingsHolder.LastKnownModState.ToList(),
                WindowSize = SettingsHolder.WindowSize
            };
            
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, Options));
        } catch (Exception ex) {
            ErrorHandler.Handle("Failed to save settings", ex);
        }
    }
}