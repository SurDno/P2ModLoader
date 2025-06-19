using System.Text.Json;
using P2ModLoader.Data;

namespace P2ModLoader.Helper;

public static class EnabledModsTracker {
    private static string? EnabledModsPath => SettingsHolder.InstallPath != null 
        ? Path.Combine(SettingsHolder.InstallPath, "Mods", "enabled.json")
        : null;
    
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    
    public static void SaveEnabledMods(IEnumerable<Mod> mods) {
        if (EnabledModsPath == null) return;
        
        try {
            var enabledMods = mods.Where(m => m.IsEnabled).OrderBy(m => m.LoadOrder).Select(m => m.FolderName).ToList();
            File.WriteAllText(EnabledModsPath, JsonSerializer.Serialize(enabledMods, Options));
            Logger.LogInfo($"Saved {enabledMods.Count} enabled mods info to enabled.json");
        } catch (Exception ex) {
            Logger.LogWarning($"Failed to save enabled mods: {ex.Message}");
        }
    }
    
    public static void ClearEnabledMods() {
        if (EnabledModsPath == null) return;
        
        try {
            File.WriteAllText(EnabledModsPath, JsonSerializer.Serialize(new List<string>(), Options));
            Logger.LogInfo("Cleared enabled mods from enabled.json");
        } catch (Exception ex) {
            Logger.LogWarning($"Failed to clear enabled mods: {ex.Message}");
        }
    }
}