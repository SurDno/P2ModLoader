using P2ModLoader.Data;

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
        return new DirectoryInfo(path.TrimEnd('/', '\\')).Name.ToLowerInvariant();
    }

    private static IEnumerable<ConflictInfo> GetConflicts(Mod mod, IEnumerable<Mod> allMods) {
        if (!mod.IsEnabled) return [];

        var allModsList = allMods.ToList();
        var conflicts = new List<ConflictInfo>();

        var modFolderName = NormalizePath(mod.FolderPath);
        var modRequirements = mod.Info.Requirements.Select(NormalizePath).ToList();

        if (modRequirements.Any()) {
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

            foreach (var otherMod in allModsList) {
                if (!otherMod.IsEnabled || otherMod == mod) continue;

                var otherModFolderName = NormalizePath(otherMod.FolderPath);

                if (!modRequirements.Contains(otherModFolderName)) {
                    var fileConflicts = GetFileConflicts(mod, otherMod, allModsList);
                    foreach (var conflict in fileConflicts) {
                        if (!IsConflictResolvedByPatch(mod, otherMod, conflict.RelativePath, allModsList)) {
                            conflicts.Add(conflict);
                        }
                    }

                    var pathConflicts = GetPaths(mod, otherMod, allModsList);
                    foreach (var conflict in pathConflicts) {
                        if (!IsConflictResolvedByPatch(mod, otherMod, conflict.RelativePath, allModsList)) {
                            conflicts.Add(conflict);
                        }
                    }
                }
            }
        } else {
            foreach (var otherMod in allModsList) {
                if (!otherMod.IsEnabled || otherMod == mod) continue;

                var fileConflicts = GetFileConflicts(mod, otherMod, allModsList);
                foreach (var conflict in fileConflicts) {
                    if (!IsConflictResolvedByPatch(mod, otherMod, conflict.RelativePath, allModsList)) {
                        conflicts.Add(conflict);
                    }
                }

                var pathConflicts = GetPaths(mod, otherMod, allModsList);
                foreach (var conflict in pathConflicts) {
                    if (!IsConflictResolvedByPatch(mod, otherMod, conflict.RelativePath, allModsList)) {
                        conflicts.Add(conflict);
                    }
                }
            }
        }

        return conflicts;
    }


    private static IEnumerable<ConflictInfo> GetFileConflicts(Mod mod, Mod otherMod, IEnumerable<Mod> allMods) {
        var extensions = new[] { "*.dll", "*.cs", "*.xml" };

        foreach (var extension in extensions) {
            var myFiles = Directory.GetFiles(mod.FolderPath, extension, SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(mod.FolderPath, path));

            var otherFiles = Directory.GetFiles(otherMod.FolderPath, extension, SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(otherMod.FolderPath, path));

            foreach (var conflictingFile in myFiles.Intersect(otherFiles, StringComparer.OrdinalIgnoreCase)) {
                var myFullPath = Path.Combine(mod.FolderPath, conflictingFile);
                var otherFullPath = Path.Combine(otherMod.FolderPath, conflictingFile);

                if (!FilesAreIdentical(myFullPath, otherFullPath)) {
                    if (!IsConflictResolvedByPatch(mod, otherMod, conflictingFile, allMods)) {
                        yield return new ConflictInfo(ConflictType.File, otherMod, conflictingFile);
                    }
                }
            }
        }
    }

    private static IEnumerable<ConflictInfo> GetPaths(Mod mod, Mod otherMod, IEnumerable<Mod> allMods) {
        var myPaths = GetAllPaths(mod.FolderPath);

        foreach (var myPath in myPaths) {
            var relativePath = Path.GetRelativePath(mod.FolderPath, myPath);
            var otherFullPath = Path.Combine(otherMod.FolderPath, relativePath);

            bool conflictExists = false;

            if (File.Exists(myPath)) {
                var pathWithoutExt = Path.Combine(
                    Path.GetDirectoryName(otherFullPath)!,
                    Path.GetFileNameWithoutExtension(otherFullPath)
                );
                if (Directory.Exists(pathWithoutExt)) {
                    conflictExists = true;
                }
            }

            if (Directory.Exists(myPath)) {
                var pathWithExt = otherFullPath + ".dll";
                if (File.Exists(pathWithExt)) {
                    conflictExists = true;
                }
            }

            if ((File.Exists(myPath) && Directory.Exists(otherFullPath)) ||
                (Directory.Exists(myPath) && File.Exists(otherFullPath))) {
                conflictExists = true;
            }

            if (conflictExists) {
                if (!IsConflictResolvedByPatch(mod, otherMod, relativePath, allMods)) {
                    yield return new ConflictInfo(ConflictType.Path, otherMod, relativePath);
                }
            }
        }
    }

    private static bool IsConflictResolvedByPatch(Mod mod1, Mod mod2, string? relativePath, IEnumerable<Mod> allMods) {
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
            m.Info.Requirements.Any() &&
            m.Info.Requirements.Select(NormalizePath).Contains(mod1Folder) &&
            m.Info.Requirements.Select(NormalizePath).Contains(mod2Folder)
        );

        foreach (var patch in patches) {
            if (relativePath != null) {
                var patchFilePath = Path.Combine(patch.FolderPath, relativePath);
                if (File.Exists(patchFilePath)) {
                    return true;
                }
            } else {
                return true;
            }
        }

        return false;
    }

    public static ModConflictDisplay GetConflictDisplay(Mod mod, IEnumerable<Mod> allMods) {
        var conflicts = GetConflicts(mod, allMods).ToList();

        var patches = conflicts.Where(c => c.Type == ConflictType.Patch).ToList();
        if (patches.Any()) {
            var patchDetails = patches
                .Select(c => $"{c.ConflictingMod.Info.Name} ({c.RelativePath})")
                .Distinct();
            return new ModConflictDisplay(PatchColor, $"Patches files from:\r\n{string.Join("\r\n", patchDetails)}");
        }

        var fileConflicts = conflicts.Where(c => c.Type == ConflictType.File).ToList();
        if (fileConflicts.Any()) {
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

    private static bool FilesAreIdentical(string path1, string path2) {
        var file1Info = new FileInfo(path1);
        var file2Info = new FileInfo(path2);

        if (file1Info.Length != file2Info.Length)
            return false;

        using var fs1 = new FileStream(path1, FileMode.Open, FileAccess.Read);
        using var fs2 = new FileStream(path2, FileMode.Open, FileAccess.Read);

        const int bufferSize = 4096;
        var buffer1 = new byte[bufferSize];
        var buffer2 = new byte[bufferSize];

        while (true) {
            var count1 = fs1.Read(buffer1, 0, bufferSize);
            var count2 = fs2.Read(buffer2, 0, bufferSize);

            if (count1 != count2)
                return false;

            if (count1 == 0)
                return true;

            for (var i = 0; i < count1; i++) {
                if (buffer1[i] != buffer2[i])
                    return false;
            }
        }
    }

    private static IEnumerable<string> GetAllPaths(string rootPath) {
        return Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Concat(Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories));
    }
}