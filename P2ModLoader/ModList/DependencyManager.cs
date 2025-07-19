using P2ModLoader.Data;
using P2ModLoader.Update;

namespace P2ModLoader.ModList;

public static class DependencyManager {
    public class DependencyValidation {
        public bool HasErrors { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
        public Color DisplayColor { get; init; } = Color.White;
    }

    private static string Normalize(string path) { 	
        return new DirectoryInfo(path.TrimEnd('/', '\\')).Name.ToLowerInvariant();
    }

    public static DependencyValidation ValidateDependencies(Mod mod, IEnumerable<Mod> allMods) { 	
        if (!mod.IsEnabled) {
            return new DependencyValidation();
        }
        
        if (!string.IsNullOrEmpty(mod.Info.MinLoaderVersion)) {
            if (VersionComparison.IsLoaderNewer(mod.Info.MinLoaderVersion)) {
                return new DependencyValidation {
                    HasErrors = true,
                    ErrorMessage = $"\r\nThis mod requires P2ModLoader version {mod.Info.MinLoaderVersion} or newer." +
                                   $"Head to Settings to update.",
                    DisplayColor = Color.Red
                };
            }
        }
        
        var activeMods = allMods.Where(m => m.IsEnabled).ToList();
        var activeFolders = activeMods.Select(m => Normalize(m.FolderPath)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (mod.Info.Requirements.Any()) {
            var missingDependencies = mod.Info.Requirements
                .Where(req => !activeFolders.Contains(Normalize(req)))
                .ToList();

            if (missingDependencies.Count != 0) {
                return new DependencyValidation {
                    HasErrors = true,
                    ErrorMessage = $"Missing dependencies: {string.Join(", ", missingDependencies)}",
                    DisplayColor = Color.Red
                };
            }
        }

        var modIndex = ModManager.Mods.ToList().IndexOf(mod);
        
        foreach (var requirement in mod.Info.Requirements) {
            var requiredMod = activeMods.First(m =>
                Normalize(m.FolderPath).Equals(Normalize(requirement),
                    StringComparison.OrdinalIgnoreCase));
                    
            var requiredModIndex = ModManager.Mods.ToList().IndexOf(requiredMod);
            if (requiredModIndex > modIndex) {
                return new DependencyValidation {
                    HasErrors = true,
                    ErrorMessage = $"Mod must be loaded after '{requiredMod.Info.Name}'",
                    DisplayColor = Color.Red
                };
            }
        }

        foreach (var loadAfterMod in mod.Info.LoadAfterMods) {
            var matchingMod = activeMods.FirstOrDefault(m =>
                Normalize(m.FolderPath).Equals(Normalize(loadAfterMod),
                    StringComparison.OrdinalIgnoreCase));

            if (matchingMod == null) continue;

            var loadAfterModIndex = ModManager.Mods.ToList().IndexOf(matchingMod);
            if (loadAfterModIndex > modIndex) {
                return new DependencyValidation {
                    HasErrors = true,
                    ErrorMessage = $"Mod must be loaded after '{matchingMod.Info.Name}'",
                    DisplayColor = Color.Red
                };
            }
        }

        return new DependencyValidation();
    }

    public static bool HasDependencyErrors(IEnumerable<Mod> mods) { 	
        return mods.Where(m => m.IsEnabled).Any(mod => ValidateDependencies(mod, mods).HasErrors);
    }
}