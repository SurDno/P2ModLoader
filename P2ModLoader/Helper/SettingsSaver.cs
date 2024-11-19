using P2ModLoader.Data;using System.Text.Json;

namespace P2ModLoader.Helper;

public static class SettingsSaver {
    private const string SETTINGS_DIRECTORY = "Settings";
    private static readonly string SettingsPath = Path.Combine(SETTINGS_DIRECTORY, "settings.json");
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = true };
    private static bool _subscribed = false;
    
    private class SavedSettings {
        public string? InstallPath { get; init; }
        public bool AllowStartupWithConflicts { get; init; }
        public bool IsPatched { get; init; }
        public bool CheckForUpdates {get; init; }
        public List<SavedModState> ModState { get; init; } = [];
    }
    
    public static void LoadSettings() {
        if (File.Exists(SettingsPath)) {
            try {
                var settings = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(SettingsPath));

                if (settings == null) return;

                SettingsHolder.InstallPath = settings.InstallPath == "null" ? null : settings.InstallPath;
                SettingsHolder.AllowStartupWithConflicts = settings.AllowStartupWithConflicts;
                SettingsHolder.IsPatched = settings.IsPatched;
                SettingsHolder.CheckForUpdates = settings.CheckForUpdates;
                SettingsHolder.LastKnownModState = settings.ModState;
            } catch (Exception ex) {
                ErrorHandler.Handle("Failed to load settings", ex);
            }
        }

        if (_subscribed) return;
        SettingsHolder.InstallPathChanged += SaveSettings;
        SettingsHolder.StartupWithConflictsChanged += SaveSettings;
        SettingsHolder.PatchStatusChanged += SaveSettings;
        SettingsHolder.ModStateChanged += SaveSettings;
        _subscribed = true;
    }
    
    private static void SaveSettings() {
        try {
            Directory.CreateDirectory(SETTINGS_DIRECTORY);
            
            var settings = new SavedSettings {
                InstallPath = SettingsHolder.InstallPath == null ? "null" : SettingsHolder.InstallPath,
                AllowStartupWithConflicts = SettingsHolder.AllowStartupWithConflicts,
                IsPatched = SettingsHolder.IsPatched,
                CheckForUpdates = SettingsHolder.CheckForUpdates,
                ModState = SettingsHolder.LastKnownModState.ToList()
            };
            
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, Options));
        } catch (Exception ex) {
            ErrorHandler.Handle("Failed to load settings", ex);
        }
    }
}