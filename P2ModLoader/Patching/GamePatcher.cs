using P2ModLoader.Data;
using P2ModLoader.Forms;
using P2ModLoader.Helper;
using P2ModLoader.Logging;
using P2ModLoader.ModList;
using P2ModLoader.Patching.Assembly;
using P2ModLoader.Patching.Assets;
using P2ModLoader.Patching.Xml;

namespace P2ModLoader.Patching;

public static class GamePatcher {
    private const string MANAGED_PATH = "Pathologic_Data/Managed/";
    private const string ASSETS_PATH = "Pathologic_Data/";
    private const string DATA_PATH = "Data/";
    private static ProgressForm? _progressForm;

    public static bool TryPatch() {
        using var perf = PerformanceLogger.Log();
        using var form = _progressForm = new ProgressForm();
        try {
            _progressForm.Show();
            Application.DoEvents();

            if (!BackupManager.TryRecoverBackups()) return false;

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
        using var perf = PerformanceLogger.Log();
        for (var index = 0; index < enabledMods.Count; index++) {
            var mod = enabledMods[index];
            Logger.Log(LogLevel.Info, $"Processing mod {mod.Info.Name}");
            _progressForm?.UpdateProgress(index, enabledMods.Count, $"Loading mod: {mod.Info.Name}");

            var managedPath = Path.Combine(mod.FolderPath, MANAGED_PATH);
            var assetsPath = Path.Combine(mod.FolderPath, ASSETS_PATH);

            if (Directory.Exists(managedPath) && !TryProcessAssemblies(managedPath, mod)) return false;
            if (Directory.Exists(assetsPath) && !ProcessAssets(assetsPath, mod)) return false;
            
            var dataPath = Path.Combine(mod.FolderPath, DATA_PATH);
            if (Directory.Exists(dataPath) && !ProcessXmlData(dataPath, mod)) return false;
        }
        return true;
    }

    private static bool TryProcessAssemblies(string modAssemblyPath, Mod mod) {
        using var perf = PerformanceLogger.Log();
        foreach (var source in Directory.GetFileSystemEntries(modAssemblyPath)) {
            var name = Path.GetFileName(source);
            var target = Path.Combine(SettingsHolder.InstallPath!, MANAGED_PATH, name);
                
            if (File.Exists(source)) {
                if (!Path.GetExtension(source).Equals(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                BackupManager.CreateBackup(target);
                _progressForm?.UpdateProgress($"Backing up: {Path.GetFileName(target)}");
                File.Copy(source, target, true);
                _progressForm?.UpdateProgress($"Copying for {mod.Info.Name}: {Path.GetFileName(target)}");
            } else if (Directory.Exists(source)) {
                var assemblyPath = target + ".dll";
                BackupManager.CreateBackup(assemblyPath);
                _progressForm?.UpdateProgress($"Backing up assembly: {name}");

                if (!PatchAssemblyWithCodeFiles(source, assemblyPath, mod))
                    return false;
            }
        }
        return true;
    }
    
    private static bool PatchAssemblyWithCodeFiles(string directory, string assemblyPath, Mod mod) {
        using var perf = PerformanceLogger.Log();
        var codeFiles = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories).ToList();
        var loadFirstFiles = mod.Info.LoadFirst.Select(f => Path.Combine(directory, f)).Where(File.Exists).ToList();
        codeFiles = loadFirstFiles.Concat(codeFiles.Except(loadFirstFiles)).ToList();

        foreach (var file in codeFiles) {
            _progressForm?.UpdateProgress($"Patching {mod.Info.Name}: {Path.GetFileName(file)}");
            if (AssemblyPatcher.PatchAssembly(assemblyPath, file)) continue;
            ErrorHandler.Handle($"Failed to patch file: {file}", null);
            return false;
        }
        return true;
    }

    private static bool ProcessAssets(string modAssetsPath, Mod mod) {
        using var perf = PerformanceLogger.Log();
        foreach (var directory in Directory.GetDirectories(modAssetsPath)) {
            if (directory.EndsWith(MANAGED_PATH.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) continue;

            var assetsFileName = Path.GetFileName(directory);
            var targetPath = Path.Combine(SettingsHolder.InstallPath!, ASSETS_PATH, assetsFileName);

            BackupManager.CreateBackup(targetPath);
            Logger.Log(LogLevel.Info, $"Backing up assets file: {targetPath}");
            _progressForm?.UpdateProgress($"Backing up assets file: {assetsFileName}");

            _progressForm?.UpdateProgress($"Updating assets file {assetsFileName} for mod {mod.Info.Name}");
            if (AssetsFilePatcher.PatchAssetsFile(targetPath, directory)) continue;
            ErrorHandler.Handle($"Failed to patch assets file {assetsFileName}", null);
            return false;
        }
        return true;
    }

    private static bool ProcessXmlData(string modDataPath, Mod mod) {
        using var perf = PerformanceLogger.Log();
        var xmlFiles = Directory.GetFiles(modDataPath, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || 
                        f.EndsWith(".xml.gz", StringComparison.OrdinalIgnoreCase));

        foreach (var xmlFile in xmlFiles) {
            var relPath = Path.GetRelativePath(modDataPath, xmlFile);
            var target = Path.Combine(SettingsHolder.InstallPath!, DATA_PATH, relPath);
            var possibleTargets = new[] {target, target + ".gz", target.Replace(".gz", "")}.Where(File.Exists).ToList();
            if (possibleTargets.Count == 0) {
                ErrorHandler.Handle($"No XML file found to override: {relPath}", null);
                return false;
            }
            target = possibleTargets.First();
            var backup =  BackupManager.CreateBackup(target);
        
            _progressForm?.UpdateProgress($"Patching XML file {relPath} for mod {mod.Info.Name}");

            if (!XmlPatcher.PatchXml(backup, xmlFile, target)) {
                ErrorHandler.Handle($"Failed to patch XML file {relPath}", null);
                return false;
            }
        }
        return true;
    }
}