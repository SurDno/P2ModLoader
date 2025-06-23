using P2ModLoader.Data;
using P2ModLoader.Helper;
using P2ModLoader.Logging;

namespace P2ModLoader.ModList;

public static class ModManager {
    private static readonly List<Mod> _mods = [];
    public static IReadOnlyList<Mod> Mods => _mods.AsReadOnly();
    
    public static event Action? ModsLoaded;

    static ModManager() {
        using var perf = PerformanceLogger.Log();
        SettingsHolder.InstallPathChanged += OnInstallPathChanged;
        
        if (SettingsHolder.InstallPath != null)
            ScanForMods();
    }

    private static void OnInstallPathChanged() {
        var newPath = SettingsHolder.InstallPath;
        if (newPath != null) {
            ScanForMods();
        } else {
            _mods.Clear();
        }
        
        ModsLoaded?.Invoke();
    }

    private static void ScanForMods() {
        using var perf = PerformanceLogger.Log();
        _mods.Clear();
        var installPath = SettingsHolder.InstallPath;

        if (string.IsNullOrEmpty(installPath)) {
            return;
        }

        var modsPath = Path.Combine(installPath, "Mods");
        var savedState = SettingsHolder.LastKnownModState;
        
        if (!Directory.Exists(modsPath)) return;

        var directories = Directory.GetDirectories(modsPath);
        
        foreach (var folder in directories) {
            if (!File.Exists(Path.Combine(folder, "ModInfo.ltx"))) continue;
            var mod = new Mod(folder);
            
            var savedMod = savedState.FirstOrDefault(s => s.ModName == mod.FolderName);
            if (savedMod != null) {
                mod.IsEnabled = savedMod.IsEnabled;
                mod.LoadOrder = savedMod.LoadOrder;
            }
            
            _mods.Add(mod);
        }

        _mods.Sort((a, b) => a.LoadOrder.CompareTo(b.LoadOrder));
    }

    public static void UpdateModOrder(int oldIndex, int newIndex) {
        using var perf = PerformanceLogger.Log();
        if (oldIndex < 0 || oldIndex >= _mods.Count || newIndex < 0 || newIndex >= _mods.Count)
            return;

        var mod = _mods[oldIndex];
        _mods.RemoveAt(oldIndex);
        _mods.Insert(newIndex, mod);

        for (var i = 0; i < _mods.Count; i++)
            _mods[i].LoadOrder = i;
            
        SettingsHolder.UpdateModState(_mods);
    }
}