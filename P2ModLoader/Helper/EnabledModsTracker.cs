using System.Text.Json;
using P2ModLoader.Data;
using P2ModLoader.Logging;

namespace P2ModLoader.Helper;

public static class EnabledModsTracker {
    private static string? EnabledModsPath => SettingsHolder.InstallPath != null 
        ? Path.Combine(SettingsHolder.InstallPath, "Mods", "enabled.json")
        : null;
    
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    
    public static void SaveEnabledMods(IEnumerable<Mod> mods) {
        using var perf = PerformanceLogger.Log();
        if (EnabledModsPath == null) return;
        
        try {
            var enabledMods = mods.Where(m => m.IsEnabled).OrderBy(m => m.LoadOrder).Select(m => m.FolderName).ToList();
            File.WriteAllText(EnabledModsPath, JsonSerializer.Serialize(enabledMods, Options));
            Logger.Log(LogLevel.Info, $"Saved {enabledMods.Count} enabled mods info to enabled.json");
        } catch (Exception ex) {
            Logger.Log(LogLevel.Warning, $"Failed to save enabled mods: {ex.Message}");
        }
    }
    
    public static void ClearEnabledMods() {
        using var perf = PerformanceLogger.Log();
        if (EnabledModsPath == null) return;
        
        try {
            File.WriteAllText(EnabledModsPath, JsonSerializer.Serialize(new List<string>(), Options));
            Logger.Log(LogLevel.Info, $"Cleared enabled mods from enabled.json");
        } catch (Exception ex) {
            Logger.Log(LogLevel.Warning, $"Failed to clear enabled mods: {ex.Message}");
        }
    }
}