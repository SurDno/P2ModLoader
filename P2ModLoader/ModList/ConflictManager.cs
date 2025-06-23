using P2ModLoader.Data;
using P2ModLoader.Logging;

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

    private static string NormalizePath(string path) {
        using var perf = PerformanceLogger.Log();
        return new DirectoryInfo(path.TrimEnd('/', '\\')).Name.ToLowerInvariant();
    }

    private static IEnumerable<ConflictInfo> GetConflicts(Mod mod, IEnumerable<Mod> allMods) {
        using var perf = PerformanceLogger.Log();
        if (!mod.IsEnabled) return [];

        var allModsList = allMods.ToList();
        var conflicts = new List<ConflictInfo>();

        var modRequirements = mod.Info.Requirements.Select(NormalizePath).ToList();

        if (modRequirements.Count != 0) {
            foreach (var requiredMod in allModsList) {
                var requiredModFolderName = NormalizePath(requiredMod.FolderPath);
                if (!requiredMod.IsEnabled || !modRequirements.Contains(requiredModFolderName)) continue;

                var filesToExclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                    "ModInfo.ltx",
                };

                var patchFiles = Directory.GetFiles(mod.FolderPath, "*.*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(mod.FolderPath, path))
                    .Where(relativePath => !filesToExclude.Contains(Path.GetFileName(relativePath)));

                conflicts.AddRange(patchFiles.Select(path => new ConflictInfo(ConflictType.Patch, requiredMod, path)));
            }

            foreach (var otherMod in allModsList.Where(otherMod => otherMod.IsEnabled && otherMod != mod)) {
                if (modRequirements.Contains(NormalizePath(otherMod.FolderPath))) continue;
                
                conflicts.AddRange(GetFileConflicts(mod, otherMod, allModsList).Where(conflict =>
                    !IsConflictResolved(mod, otherMod, conflict.RelativePath, allModsList)));
                conflicts.AddRange(GetPaths(mod, otherMod, allModsList)
                    .Where(с => !IsConflictResolved(mod, otherMod, с.RelativePath, allModsList)));
            }
        } else {
            foreach (var otherMod in allModsList.Where(otherMod => otherMod.IsEnabled && otherMod != mod)) {
                conflicts.AddRange(GetFileConflicts(mod, otherMod, allModsList)
                    .Where(conflict => !IsConflictResolved(mod, otherMod, conflict.RelativePath, allModsList)));
                conflicts.AddRange(GetPaths(mod, otherMod, allModsList).Where(conflict =>
                    !IsConflictResolved(mod, otherMod, conflict.RelativePath, allModsList)));
            }
        }

        return conflicts;
    }


    private static IEnumerable<ConflictInfo> GetFileConflicts(Mod mod, Mod otherMod, IEnumerable<Mod> allMods) {
        using var perf = PerformanceLogger.Log();
        var extensions = new[] { "*.dll", "*.cs", "*.xml", "*.xml.gz" };
    
        foreach (var extension in extensions) {
            var myFiles = Directory.GetFiles(mod.FolderPath, extension, SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(mod.FolderPath, path));
            
            var otherFiles = Directory.GetFiles(otherMod.FolderPath, extension, SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(otherMod.FolderPath, path));
            
            foreach (var conflictingFile in myFiles.Intersect(otherFiles, StringComparer.OrdinalIgnoreCase)) {
                var myFullPath = Path.Combine(mod.FolderPath, conflictingFile);
                var otherFullPath = Path.Combine(otherMod.FolderPath, conflictingFile);
            
                if (!FileConflictResolution.AreFilesCompatible(myFullPath, otherFullPath) && 
                    !IsConflictResolved(mod, otherMod, conflictingFile, allMods)) {
                    yield return new ConflictInfo(ConflictType.File, otherMod, conflictingFile);
                }
            }
        }
    }

    private static IEnumerable<ConflictInfo> GetPaths(Mod mod, Mod otherMod, IEnumerable<Mod> allMods) {
        using var perf = PerformanceLogger.Log();
        var myPaths = GetAllPaths(mod.FolderPath);

        foreach (var myPath in myPaths) {
            var relativePath = Path.GetRelativePath(mod.FolderPath, myPath);
            var otherFullPath = Path.Combine(otherMod.FolderPath, relativePath);

            var conflictExists = false;

            if (File.Exists(myPath)) {
                var pathWithoutExt = Path.Combine(
                    Path.GetDirectoryName(otherFullPath)!,
                    Path.GetFileNameWithoutExtension(otherFullPath)
                );
                if (Directory.Exists(pathWithoutExt)) 
                    conflictExists = true;
            }

            if (Directory.Exists(myPath)) {
                var pathWithExt = otherFullPath + ".dll";
                if (File.Exists(pathWithExt)) 
                    conflictExists = true;
            }

            if ((File.Exists(myPath) && Directory.Exists(otherFullPath)) ||
                (Directory.Exists(myPath) && File.Exists(otherFullPath))) {
                conflictExists = true;
            }

            if (!conflictExists) continue;
            if (!IsConflictResolved(mod, otherMod, relativePath, allMods))
                yield return new ConflictInfo(ConflictType.Path, otherMod, relativePath);
        }
    }

    private static bool IsConflictResolved(Mod mod1, Mod mod2, string? relativePath, IEnumerable<Mod> allMods) {
        using var perf = PerformanceLogger.Log();
        var mod1Folder = NormalizePath(mod1.FolderPath);
        var mod2Folder = NormalizePath(mod2.FolderPath);

        var mod1Requirements = mod1.Info.Requirements.Select(NormalizePath).ToList();
        if (mod1Requirements.Contains(mod2Folder)) {
            if (relativePath != null) {
                var patchFilePath = Path.Combine(mod1.FolderPath, relativePath);
                if (File.Exists(patchFilePath))
                    return true;
            } else {
                return true;
            }
        }

        var mod2Requirements = mod2.Info.Requirements.Select(NormalizePath).ToList();
        if (mod2Requirements.Contains(mod1Folder)) {
            if (relativePath != null) {
                var patchFilePath = Path.Combine(mod2.FolderPath, relativePath);
                if (File.Exists(patchFilePath))
                    return true;
            } else {
                return true;
            }
        }

        var patches = allMods.Where(m =>
            m.IsEnabled &&
            m.Info.Requirements.Count != 0 &&
            m.Info.Requirements.Select(NormalizePath).Contains(mod1Folder) &&
            m.Info.Requirements.Select(NormalizePath).Contains(mod2Folder)
        );

        return patches.Any(patch => relativePath == null || File.Exists(Path.Combine(patch.FolderPath, relativePath)));
    }

    public static ModConflictDisplay GetConflictDisplay(Mod mod, IEnumerable<Mod> allMods) {
        using var perf = PerformanceLogger.Log();
        var conflicts = GetConflicts(mod, allMods).ToList();

        var patches = conflicts.Where(c => c.Type == ConflictType.Patch).ToList();
        if (patches.Count != 0) {
            var patchDetails = patches
                .Select(c => $"{c.ConflictingMod.Info.Name} ({c.RelativePath})")
                .Distinct();
            return new ModConflictDisplay(PatchColor, $"Patches files from:\r\n{string.Join("\r\n", patchDetails)}");
        }

        var fileConflicts = conflicts.Where(c => c.Type == ConflictType.File).ToList();
        if (fileConflicts.Count != 0) {
            var conflictDetails = fileConflicts
                .Select(c => $"{c.ConflictingMod.Info.Name} ({c.RelativePath})")
                .Distinct();
            return new ModConflictDisplay(FileConflictColor,
                $"File conflicts:\r\n{string.Join("\r\n", conflictDetails)}");
        }

        var pathConflicts = conflicts.Where(c => c.Type == ConflictType.Path).ToList();
        if (pathConflicts.Count != 0) {
            var pathDetails = pathConflicts
                .Select(c => $"{c.ConflictingMod.Info.Name} ({c.RelativePath})")
                .Distinct();
            return new ModConflictDisplay(PathColor, $"Path conflicts:\r\n{string.Join("\r\n", pathDetails)}");
        }

        return new ModConflictDisplay(NoConflictColor, string.Empty);

    }

    private static IEnumerable<string> GetAllPaths(string rootPath) {
        using var perf = PerformanceLogger.Log();
        return Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Concat(Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories));
    }
}