using System.Diagnostics;
using P2ModLoader.Forms;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using P2ModLoader.AssemblyPatching;
using P2ModLoader.ModList;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace P2ModLoader.Helper {
    public static class GameLauncher {
        private const string MANAGED_PATH = "Pathologic_Data/Managed/";
        private const string ASSETS_PATH = "Pathologic_Data/";
        private const string EXE_PATH = "Pathologic.exe";
        private static ProgressForm? _progressForm;

        public static bool TryPatch() {
            using var form = _progressForm = new ProgressForm();
            try {
                _progressForm.Show();
                Application.DoEvents();

                if(!BackupManager.TryRecoverBackups()) return false;

                var enabledMods = ModManager.Mods.Where(m => m.IsEnabled).ToList();
                var currentMod = 0;

                Logger.LogInfo("Preparing to patch...");
                foreach (var mod in enabledMods) {
                    currentMod++;
                    Logger.LogInfo($"Iterating through mod {mod.Info.Name}");
                    _progressForm?.UpdateProgress(currentMod, enabledMods.Count, $"Loading mod: {mod.Info.Name}");

                    var modAssemblyPath = Path.Combine(mod.FolderPath, MANAGED_PATH);

                    if (!Directory.Exists(modAssemblyPath)) continue;
                    BackupAssemblies(modAssemblyPath);
                    var success = UpdateAssemblies(modAssemblyPath, mod.Info.Name);
                    if (!success) return false;
                }
                
                Logger.LogInfo($"Finished loading {enabledMods.Count} mods.");
                SettingsHolder.IsPatched = true;
                
                return true;
            } catch (Exception ex) {
                ErrorHandler.Handle("Error during patching", ex);
                return false;
            }
        }

        public static void LaunchExe() {
            if (SettingsHolder.InstallPath == null)
                return;

            if (!SettingsHolder.IsPatched && !TryPatch()) return;

            var gameExecutable = Path.Combine(SettingsHolder.InstallPath!, EXE_PATH);

            Process.Start(new ProcessStartInfo {
                FileName = gameExecutable,
                WorkingDirectory = Path.GetDirectoryName(gameExecutable)
            });
        }

        public static void LaunchSteam() {
            if (SettingsHolder.InstallPath == null)
                return;

            if (!SettingsHolder.IsPatched && !TryPatch()) return;
            
            var steamProcess = new ProcessStartInfo {
                FileName = Path.Combine(InstallationLocator.FindSteam()!, InstallationLocator.STEAM_EXE),
                Arguments = "-applaunch 505230 -StraightIntoFreemode"
            };

            Process.Start(steamProcess);
        }

        private static void BackupAssemblies(string modAssemblyPath) {
            var directories = Directory.GetDirectories(modAssemblyPath);
            for (int i = 0; i < directories.Length; i++) {
                var directory = directories[i];
                var assemblyName = Path.GetFileName(directory);
                var assemblyPath = Path.Combine(SettingsHolder.InstallPath!, MANAGED_PATH, assemblyName) + ".dll";
                Logger.LogInfo($"Found directory {directory}, {assemblyPath}.");
                BackupManager.CreateBackup(assemblyPath);
                _progressForm?.UpdateProgress($"Backing up assembly: {assemblyName}");
            }

            var dllFiles = Directory.GetFiles(modAssemblyPath, "*.dll", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < dllFiles.Length; i++) {
                var assemblyDll = dllFiles[i];
                var assemblyName = Path.GetFileName(assemblyDll);
                var originalDll = Path.Combine(SettingsHolder.InstallPath!, MANAGED_PATH, assemblyName);
                BackupManager.CreateBackup(originalDll);
                _progressForm?.UpdateProgress($"Backing up assembly: {assemblyName}");
            }
        }

        private static bool UpdateAssemblies(string modAssemblyPath, string modName) {
            var dllFiles = Directory.GetFiles(modAssemblyPath, "*.dll", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < dllFiles.Length; i++) {
                var assemblyDll = dllFiles[i];
                var assemblyName = Path.GetFileName(assemblyDll);
                var originalDll = Path.Combine(SettingsHolder.InstallPath!, MANAGED_PATH, assemblyName);
                File.Copy(assemblyDll, originalDll, true);
                _progressForm?.UpdateProgress($"Copying assemblies for {modName}: {assemblyName}");
            }

            var assemblies = Directory.GetDirectories(modAssemblyPath);
            foreach (var assembly in assemblies) {
                var assemblyName = Path.GetFileName(assembly);
                var assemblyPath = Path.Combine(SettingsHolder.InstallPath!, MANAGED_PATH, assemblyName) + ".dll";

                var codeFiles = Directory.GetFiles(assembly, "*.cs", SearchOption.AllDirectories).ToList();
                var pendingFiles = new List<string>(codeFiles);

                do {
                    var progressMade = false;
                    var filesToRetry = new List<string>();

                    foreach (var codeFile in pendingFiles) {
                        _progressForm?.UpdateProgress(
                            $"Patching {modName}: {assemblyName} with {Path.GetFileName(codeFile)}");
                        bool success = AssemblyPatcher.PatchAssembly(assemblyPath, codeFile);

                        if (success) {
                            progressMade = true;
                        } else {
                            filesToRetry.Add(codeFile);
                        }
                    }

                    if (!progressMade && filesToRetry.Any()) {
                        var str = filesToRetry.Aggregate("Unable to patch some files:",
                            (cur, file) => cur + $"- {file}");
                        ErrorHandler.Handle(str, null);
                        return false;
                    }

                    pendingFiles = filesToRetry;
                } while (pendingFiles.Any());
            }

            
            return true;
        }
    }
}