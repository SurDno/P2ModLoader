using P2ModLoader.Data;
using P2ModLoader.Helper;
using P2ModLoader.Logging;

namespace P2ModLoader.ModList;

public static class ModManager {
    private static readonly List<Mod> _allMods = [];
    
    public static IReadOnlyList<Mod> Mods => _allMods
        .Where(m => {
            var install = SettingsHolder.SelectedInstall;
            if (install == null) return false;
            return m.IsCompatibleWith(install) && 
                   m.FolderPath.StartsWith(install.ModsPath, StringComparison.OrdinalIgnoreCase);
        })
        .ToList();
    
    public static IReadOnlyList<Mod> GetModsForInstall(Install install) {
        return _allMods
            .Where(m => m.IsCompatibleWith(install) && 
                       m.FolderPath.StartsWith(install.ModsPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
    
    public static event Action? ModsLoaded;

    static ModManager() { 	
        SettingsHolder.InstallPathChanged += OnInstallPathChanged;
        SettingsHolder.InstallsChanged += ScanAllInstalls;
        
        ScanAllInstalls();
    }

    private static void OnInstallPathChanged() => ModsLoaded?.Invoke();

    private static void ScanAllInstalls() { 	
        _allMods.Clear();
        foreach (var install in SettingsHolder.Installs) 
            ScanModsForInstall(install);
        ModsLoaded?.Invoke();
    }

    public static void ScanModsForInstall(Install install) { 	
        _allMods.RemoveAll(m => m.FolderPath.StartsWith(install.ModsPath, StringComparison.OrdinalIgnoreCase));
        
        var modsPath = install.ModsPath;
        if (!Directory.Exists(modsPath)) {
            Logger.Log(LogLevel.Info, $"Mods directory does not exist for {install.DisplayName}: {modsPath}");
            return;
        }

        var directories = Directory.GetDirectories(modsPath);
        var savedState = install.ModState;
        
        foreach (var folder in directories) {
            if (!File.Exists(Path.Combine(folder, "ModInfo.ltx"))) continue;
            var mod = new Mod(folder);
            
            var savedMod = savedState.FirstOrDefault(s => s.ModName == mod.FolderName);
            if (savedMod != null) {
                mod.IsEnabled = savedMod.IsEnabled;
                mod.LoadOrder = savedMod.LoadOrder;
            }
            
            _allMods.Add(mod);
        }

        _allMods.Sort((a, b) => a.LoadOrder.CompareTo(b.LoadOrder));
        
        var incompatible = _allMods
            .Where(m => m.FolderPath.StartsWith(install.ModsPath, StringComparison.OrdinalIgnoreCase) && 
                       !m.IsCompatibleWith(install))
            .ToList();
        
        if (incompatible.Any()) {
            Logger.Log(LogLevel.Info, $"Found {incompatible.Count} mods incompatible with {install.DisplayName}");
        }
    }

    public static void UpdateModOrder(int oldIndex, int newIndex) { 	
        var visibleMods = Mods.ToList();
        if (oldIndex < 0 || oldIndex >= visibleMods.Count || newIndex < 0 || newIndex >= visibleMods.Count)
            return;

        var mod = visibleMods[oldIndex];
        visibleMods.RemoveAt(oldIndex);
        visibleMods.Insert(newIndex, mod);

        for (var i = 0; i < visibleMods.Count; i++)
            visibleMods[i].LoadOrder = i;
    
        _allMods.Sort((a, b) => a.LoadOrder.CompareTo(b.LoadOrder));
    
        SettingsHolder.UpdateModState(visibleMods);
        //ModsLoaded?.Invoke(); 
    }
}