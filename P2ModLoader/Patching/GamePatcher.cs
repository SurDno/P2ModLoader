using P2ModLoader.Data;
using P2ModLoader.Forms;
using P2ModLoader.Helper;
using P2ModLoader.Logging;
using P2ModLoader.ModList;
using P2ModLoader.Patching.Assembly;
using P2ModLoader.Patching.Assets;
using P2ModLoader.Patching.Backups;
using P2ModLoader.Patching.Xml;

namespace P2ModLoader.Patching;

public static class GamePatcher {
    private const string DATA_PATH = "Data/";
    private static ProgressForm? _progressForm;

    public static bool TryPatch() { 	
        try {
            if (!BackupManager.TryRecoverBackups()) return false;
            
            using var form = _progressForm = new ProgressForm();
            _progressForm.Show();
            Application.DoEvents();

            var enabledMods = ModManager.Mods.Where(m => m.IsEnabled).ToList();
            EnabledModsTracker.SaveEnabledMods(enabledMods);
            Logger.Log(LogLevel.Info, $"Preparing to patch...");

            if (!TryProcessMods(enabledMods)) return false;
            Logger.Log(LogLevel.Info, $"Finished loading {enabledMods.Count} mods.");
            SettingsHolder.IsPatched = true;
            return true;
        } catch (Exception ex) {
            ErrorHandler.Handle("Error during patching", ex);
            return false;
        }
    }

    private static bool TryProcessMods(List<Mod> enabledMods) { 	
        for (var index = 0; index < enabledMods.Count; index++) {
            var mod = enabledMods[index];
            Logger.Log(LogLevel.Info, $"Processing mod {mod.Info.Name}");
            _progressForm?.UpdateProgress(index, enabledMods.Count, $"Loading mod: {mod.Info.Name}");

            var managedPath = Path.Combine(mod.FolderPath, SettingsHolder.SelectedInstall!.ManagedPath);
            var assetsPath = Path.Combine(mod.FolderPath, SettingsHolder.SelectedInstall!.AssetsPath);
            var dataPath = Path.Combine(mod.FolderPath, DATA_PATH);

            if (Directory.Exists(managedPath) && !TryProcessAssemblies(managedPath, mod)) return false;
            if (Directory.Exists(assetsPath) && !ProcessAssets(assetsPath, mod)) return false;
            if (Directory.Exists(dataPath) && !ProcessXmlData(dataPath, mod)) return false;
        }
        return true;
    }

    private static bool TryProcessAssemblies(string modAssemblyPath, Mod mod) { 	
        foreach (var source in Directory.GetFileSystemEntries(modAssemblyPath)) {
            var name = Path.GetFileName(source);
            var target = Path.Combine(SettingsHolder.InstallPath!, SettingsHolder.SelectedInstall!.FullManagedPath, name);
                
            if (File.Exists(source)) {
                if (!Path.GetExtension(source).Equals(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                var backup = BackupManager.CreateBackupOrTrack(target);
                _progressForm?.UpdateProgress($"Backing up: {Path.GetFileName(target)}");
                try {
                    File.Copy(source, target, true);
                    BackupManager.SavePatchedFileHash(target);
                } catch {
                    BackupManager.DeleteBackupIfExists(backup);
                    throw;
                }
                _progressForm?.UpdateProgress($"Copying for {mod.Info.Name}: {Path.GetFileName(target)}");
            } else if (Directory.Exists(source)) {
                var assemblyPath = target + ".dll";
                var backup = BackupManager.CreateBackupOrTrack(assemblyPath);
                try {
                    if (!PatchAssemblyWithCodeFiles(source, assemblyPath, mod)) {
                        BackupManager.DeleteBackupIfExists(backup);
                        return false;
                    }
                    BackupManager.SavePatchedFileHash(assemblyPath);
                } catch {
                    BackupManager.DeleteBackupIfExists(backup);
                    throw;
                }
            }
        }
        return true;
    }
    
    private static bool PatchAssemblyWithCodeFiles(string directory, string assemblyPath, Mod mod) { 	
        var codeFiles = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories).ToList();
        var loadFirstFiles = mod.Info.LoadFirst.Select(f => Path.Combine(directory, f)).Where(File.Exists).ToList();
        codeFiles = loadFirstFiles.Concat(codeFiles.Except(loadFirstFiles)).ToList();

        for (var index = 0; index < codeFiles.Count; index++) {
            var file = codeFiles[index];
            _progressForm?.UpdateProgress($"Patching {mod.Info.Name}: {Path.GetFileName(file)}");
            if (AssemblyPatcher.PatchAssembly(assemblyPath, file, mod)) continue;
            ErrorHandler.Handle($"Failed to patch file: {file}", null);
            return false;
        }

        return true;
    }

    private static bool ProcessAssets(string modAssetsPath, Mod mod) {
        var assetsByFile = new Dictionary<string, List<string>>();

        foreach (var directory in Directory.GetDirectories(modAssetsPath)) {
            if (directory.EndsWith(SettingsHolder.SelectedInstall!.ManagedPath.TrimEnd('/'),
                    StringComparison.OrdinalIgnoreCase)) continue;

            var assetsFileName = Path.GetFileName(directory);
            var targetPath = Path.Combine(SettingsHolder.InstallPath!, 
                SettingsHolder.SelectedInstall!.AssetsPath, assetsFileName);

            if (!assetsByFile.ContainsKey(targetPath))
                assetsByFile[targetPath] = [];
    
            assetsByFile[targetPath].Add(directory);
        }

        foreach (var (targetPath, modFolders) in assetsByFile) {
            var backup = BackupManager.CreateBackupOrTrack(targetPath);
            Logger.Log(LogLevel.Info, $"Backing up assets file: {targetPath}");
            _progressForm?.UpdateProgress($"Updating {Path.GetFileName(targetPath)} for mod {mod.Info.Name}");
    
            try {
                if (!AssetsFilePatcher.PatchAssetsFile(targetPath, modFolders)) {
                    BackupManager.DeleteBackupIfExists(backup);
                    ErrorHandler.Handle($"Failed to patch assets file {Path.GetFileName(targetPath)}", null);
                    return false;
                }
                BackupManager.SavePatchedFileHash(targetPath);
            } catch {
                BackupManager.DeleteBackupIfExists(backup);
                throw;
            }
        }
        return true;
    }


    private static bool ProcessXmlData(string modDataPath, Mod mod) { 	
        var xmlFiles = Directory.GetFiles(modDataPath, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || 
                        f.EndsWith(".xml.gz", StringComparison.OrdinalIgnoreCase));

        foreach (var xmlFile in xmlFiles) {
            var relPath = Path.GetRelativePath(modDataPath, xmlFile);
            var target = Path.Combine(SettingsHolder.InstallPath!, DATA_PATH, relPath);
            var possibleTargets = new[] {target, target + ".gz", target.Replace(".gz", "")}.Where(File.Exists).ToList();
        
            if (possibleTargets.Count == 0) {
                var backup = BackupManager.CreateBackupOrTrack(target);
                Logger.Log(LogLevel.Info, $"Adding new XML file: {relPath}");
                _progressForm?.UpdateProgress($"Adding new XML file {relPath} for mod {mod.Info.Name}");
            
                try {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(xmlFile, target, true);
                    BackupManager.SavePatchedFileHash(target);
                } catch {
                    BackupManager.DeleteBackupIfExists(backup);
                    throw;
                }
                continue;
            }
        
            target = possibleTargets.First();
            var backup2 = BackupManager.CreateBackupOrTrack(target);
    
            _progressForm?.UpdateProgress($"Patching XML file {relPath} for mod {mod.Info.Name}");

            try {
                if (!XmlPatcher.PatchXml(backup2, xmlFile, target)) {
                    BackupManager.DeleteBackupIfExists(backup2);
                    ErrorHandler.Handle($"Failed to patch XML file {relPath}", null);
                    return false;
                }
                BackupManager.SavePatchedFileHash(target);
            } catch {
                BackupManager.DeleteBackupIfExists(backup2);
                throw;
            }
        }
        return true;
    }
}