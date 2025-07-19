using P2ModLoader.Data;
using P2ModLoader.Forms;

namespace P2ModLoader.ModList;

public enum ConflictType {
    None,
    Path,
    File,
    Patch
}

public class ConflictInfo(ConflictType type, Mod conflictingMod, string relativePath) {
    public ConflictType Type { get; } = type;
    public Mod ConflictingMod { get; } = conflictingMod;
    public string RelativePath { get; } = relativePath;
}

public class ModConflictDisplay(Color backgroundColor, string toolTip) {
    public Color BackgroundColor { get; } = backgroundColor;
    public string ToolTip { get; } = toolTip;
}

public static class ConflictManager {
    private static readonly Color NoConflictColor = SystemColors.Window;
    private static readonly Color FileConflictColor = Color.LightCoral;
    private static readonly Color PathColor = Color.LightYellow;
    private static readonly Color PatchColor = Color.LightGreen;

    private static readonly Dictionary<string, List<ConflictInfo>> _conflictCache = new();

    public static void ClearCache() => _conflictCache.Clear();

    public static void PrecomputeAllConflicts(IEnumerable<Mod> allMods) { 	
        _conflictCache.Clear();

        var modsList = allMods.ToList();
        
        using var progressForm = new ProgressForm();
        progressForm.Text = "Preloading mods...";
        progressForm.TitleText = "Analysing mod conflicts.";
        progressForm.Show();
        Application.DoEvents();

        for (var index = 0; index < modsList.Count; index++) {
            var mod = modsList[index];
            var key = NormalizePath(mod.FolderPath);
            progressForm.UpdateProgress(index, modsList.Count, $"Preloading conflicts for mod {mod.FolderName}");
            _conflictCache[key] = ComputeAllConflicts(mod, modsList);
        }
    }

    public static ModConflictDisplay GetConflictDisplay(Mod mod, IEnumerable<Mod> allMods) { 	

        var key = NormalizePath(mod.FolderPath);
        if (!_conflictCache.TryGetValue(key, out var allConflicts))
            return new ModConflictDisplay(NoConflictColor, string.Empty);

        var enabledMods = allMods.Where(m => m.IsEnabled).ToList();
        var filtered = allConflicts
            .Where(c => mod.IsEnabled && c.ConflictingMod.IsEnabled &&
                        !IsConflictResolved(mod, c.ConflictingMod, c.RelativePath, enabledMods))
            .ToList();

        if (filtered.Count == 0)
            return new ModConflictDisplay(NoConflictColor, string.Empty);

        if (filtered.Any(c => c.Type == ConflictType.Patch)) {
            var details = filtered.Where(c => c.Type == ConflictType.Patch)
                .Select(c => $"{c.ConflictingMod.Info.Name} ({c.RelativePath})")
                .Distinct();
            return new ModConflictDisplay(PatchColor, $"Patches files from:\r\n{string.Join("\r\n", details)}");
        }

        if (filtered.Any(c => c.Type == ConflictType.File)) {
            var details = filtered.Where(c => c.Type == ConflictType.File)
                .Select(c => $"{c.ConflictingMod.Info.Name} ({c.RelativePath})")
                .Distinct();
            return new ModConflictDisplay(FileConflictColor, $"File conflicts:\r\n{string.Join("\r\n", details)}");
        }

        if (filtered.Any(c => c.Type == ConflictType.Path)) {
            var details = filtered.Where(c => c.Type == ConflictType.Path)
                .Select(c => $"{c.ConflictingMod.Info.Name} ({c.RelativePath})")
                .Distinct();
            return new ModConflictDisplay(PathColor, $"Path conflicts:\r\n{string.Join("\r\n", details)}");
        }

        return new ModConflictDisplay(NoConflictColor, string.Empty);
    }

    private static List<ConflictInfo> ComputeAllConflicts(Mod mod, List<Mod> allMods) { 	
        var conflicts = new List<ConflictInfo>();
        var modRequirements = mod.Info.Requirements.Select(NormalizePath).ToList();

        if (modRequirements.Count != 0) {
            foreach (var requiredMod in allMods) {
                var requiredName = NormalizePath(requiredMod.FolderPath);
                if (!modRequirements.Contains(requiredName)) continue;

                var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ModInfo.ltx" };
                var patchFiles = Directory.GetFiles(mod.FolderPath, "*.*", SearchOption.AllDirectories)
                    .Select(p => Path.GetRelativePath(mod.FolderPath, p))
                    .Where(rp => !exclude.Contains(Path.GetFileName(rp)));

                conflicts.AddRange(patchFiles.Select(p => new ConflictInfo(ConflictType.Patch, requiredMod, p)));
            }

            foreach (var other in allMods.Where(m => m != mod)) {
                if (modRequirements.Contains(NormalizePath(other.FolderPath))) continue;
                conflicts.AddRange(GetFileConflicts(mod, other));
                conflicts.AddRange(GetPaths(mod, other));
            }
        } else {
            foreach (var other in allMods.Where(m => m != mod)) {
                conflicts.AddRange(GetFileConflicts(mod, other));
                conflicts.AddRange(GetPaths(mod, other));
            }
        }

        return conflicts;
    }

    private static IEnumerable<ConflictInfo> GetFileConflicts(Mod mod, Mod otherMod) { 	
        var extensions = new[] { "*.dll", "*.cs", "*.xml", "*.xml.gz" };

        foreach (var ext in extensions) {
            var myFiles = Directory.GetFiles(mod.FolderPath, ext, SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(mod.FolderPath, f));

            var otherFiles = Directory.GetFiles(otherMod.FolderPath, ext, SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(otherMod.FolderPath, f));

            foreach (var f in myFiles.Intersect(otherFiles, StringComparer.OrdinalIgnoreCase)) {
                var path1 = Path.Combine(mod.FolderPath, f);
                var path2 = Path.Combine(otherMod.FolderPath, f);

                if (!FileConflictResolution.AreFilesCompatible(path1, path2)) {
                    yield return new ConflictInfo(ConflictType.File, otherMod, f);
                }
            }
        }
    }

    private static IEnumerable<ConflictInfo> GetPaths(Mod mod, Mod otherMod) { 	
        foreach (var path in GetAllPaths(mod.FolderPath)) {
            var rel = Path.GetRelativePath(mod.FolderPath, path);
            var other = Path.Combine(otherMod.FolderPath, rel);

            var conflict = false;

            if (File.Exists(path) && Directory.Exists(other)) conflict = true;
            else if (Directory.Exists(path) && File.Exists(other)) conflict = true;
            else if (File.Exists(path)) {
                var noExt = Path.Combine(Path.GetDirectoryName(other)!, Path.GetFileNameWithoutExtension(other));
                if (Directory.Exists(noExt)) conflict = true;
            } else if (Directory.Exists(path)) {
                var withExt = other + ".dll";
                if (File.Exists(withExt)) conflict = true;
            }

            if (conflict) {
                yield return new ConflictInfo(ConflictType.Path, otherMod, rel);
            }
        }
    }

    private static IEnumerable<string> GetAllPaths(string root) { 	
        return Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
            .Concat(Directory.GetDirectories(root, "*", SearchOption.AllDirectories));
    }

    private static bool IsConflictResolved(Mod mod1, Mod mod2, string? relPath, IEnumerable<Mod> enabledMods) { 	
        var name1 = NormalizePath(mod1.FolderPath);
        var name2 = NormalizePath(mod2.FolderPath);

        var req1 = mod1.Info.Requirements.Select(NormalizePath).ToList();
        if (req1.Contains(name2)) {
            if (relPath == null || File.Exists(Path.Combine(mod1.FolderPath, relPath))) return true;
        }

        var req2 = mod2.Info.Requirements.Select(NormalizePath).ToList();
        if (req2.Contains(name1)) {
            if (relPath == null || File.Exists(Path.Combine(mod2.FolderPath, relPath))) return true;
        }

        return enabledMods.Any(p =>
            p.Info.Requirements.Select(NormalizePath).Contains(name1) &&
            p.Info.Requirements.Select(NormalizePath).Contains(name2) &&
            (relPath == null || File.Exists(Path.Combine(p.FolderPath, relPath)))
        );
    }

    private static string NormalizePath(string path) { 	
        return new DirectoryInfo(path.TrimEnd('/', '\\')).Name.ToLowerInvariant();
    }
}
