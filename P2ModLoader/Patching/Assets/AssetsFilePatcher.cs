using AssetsTools.NET;
using AssetsTools.NET.Extra;
using P2ModLoader.Helper;
using P2ModLoader.Logging;
using P2ModLoader.Patching.Backups;
using FileInstance = AssetsTools.NET.Extra.AssetsFileInstance;

namespace P2ModLoader.Patching.Assets {
    public static class AssetsFilePatcher {
        private static readonly List<(long pathId, string path)> ResourcePaths = new();

        private static readonly List<AssetTypeHandlerBase> AssetHandlers =
            [new TextAssetHandler(), new AudioAssetHandler(), new TextureAssetHandler()];

        public static bool PatchAssetsFile(string assetsFilePath, string modAssetsFolder) {
            AssetsManager? manager = null;
            FileInstance? assetsFileInstance = null;

            var outputFile = assetsFilePath + ".new";

            try {
                manager = new AssetsManager();
                manager.LoadClassPackage("Resources/classdata.tpk");
                assetsFileInstance = manager.LoadAssetsFile(assetsFilePath);
                manager.LoadClassDatabaseFromPackage(assetsFileInstance.file.Metadata.UnityVersion);

                if (Path.GetFileName(assetsFilePath).Equals("resources.assets", StringComparison.OrdinalIgnoreCase))
                    ReadResourcePaths(Path.GetDirectoryName(Path.GetDirectoryName(assetsFilePath))!);

                int initialPathCount = ResourcePaths.Count;
                ProcessModAssetFolder(manager, assetsFileInstance, modAssetsFolder, "");
                var resourcePathsModified = ResourcePaths.Count > initialPathCount;

                using (var writer = new AssetsFileWriter(outputFile))
                    assetsFileInstance.file.Write(writer, -1);
                assetsFileInstance.file.Close();
                assetsFileInstance = null;

                BackupManager.CreateBackupOrTrack(assetsFilePath);
                File.Copy(outputFile, assetsFilePath, true);

                if (resourcePathsModified)
                    WriteResourcePaths(Path.GetDirectoryName(Path.GetDirectoryName(assetsFilePath))!);
                
                Logger.Log(LogLevel.Info, $"Successfully patched {assetsFilePath}");
                return true;
            } catch (Exception ex) {
                ErrorHandler.Handle("Error patching assets file", ex);
                return false;
            } finally {
                assetsFileInstance?.file.Close();
                manager?.UnloadAll();
                if (File.Exists(outputFile))
                    File.Delete(outputFile);
            }
        }

        private static void WriteResourcePaths(string gamePath) {
            try {
                var am = new AssetsManager();
                am.LoadClassPackage("Resources/classdata.tpk");

                var ggmPath = Path.Combine(SettingsHolder.SelectedInstall!.FullAssetsPath, "globalgamemanagers");
                if (!File.Exists(ggmPath)) {
                    Logger.Log(LogLevel.Error, $"Could not find globalgamemanagers at {ggmPath}");
                    return;
                }

                var ggm = am.LoadAssetsFile(ggmPath);
                am.LoadClassDatabaseFromPackage(ggm.file.Metadata.UnityVersion);

                var resourceManagerAssets = ggm.file.GetAssetsOfType(AssetClassID.ResourceManager);
                if (resourceManagerAssets.Count == 0) {
                    Logger.Log(LogLevel.Error, $"No ResourceManager asset found in globalgamemanagers");
                    return;
                }
                
                int index = ggm.file.Metadata.Externals.FindIndex(e => e.PathName == "resources.assets");
                if (index == -1) {
                    var newExternal = new AssetsFileExternal {
                        PathName = "resources.assets"
                    };
                    ggm.file.Metadata.Externals.Add(newExternal);
                    index = ggm.file.Metadata.Externals.Count - 1;
                }
                int fileId = index + 1; 

                var rsrcInfo = resourceManagerAssets[0];
                var rsrcBf = am.GetBaseField(ggm, rsrcInfo);

                var m_Container = rsrcBf["m_Container.Array"];

                var existingEntries = new HashSet<(long pathId, string path)>();
                foreach (var data in m_Container.Children) {
                    var pathId = data[1]["m_PathID"].AsLong;
                    var path = data[0].AsString;
                    existingEntries.Add((pathId, path));
                }

                int addedCount = 0;
                foreach (var entry in ResourcePaths) {
                    if (!existingEntries.Contains(entry)) {
                        var pair = ValueBuilder.DefaultValueFieldFromArrayTemplate(m_Container);
                        pair[0].AsString = entry.path;
                        pair[1]["m_FileID"].AsInt = fileId;
                        pair[1]["m_PathID"].AsLong = entry.pathId;
                        m_Container.Children.Add(pair);
                        addedCount++;
                        Logger.Log(LogLevel.Info, $"Added new resource path: {entry.path} -> PathID {entry.pathId}");
                    }
                }

                if (addedCount == 0) {
                    Logger.Log(LogLevel.Info, $"No new resource paths to add");
                    ggm.file.Close();
                    am.UnloadAll();
                    return;
                }

                rsrcInfo.SetNewData(rsrcBf);

                var outputFile = ggmPath + ".new";
                using (var writer = new AssetsFileWriter(outputFile)) {
                    ggm.file.Write(writer, -1);
                }

                ggm.file.Close();
                am.UnloadAll();

                BackupManager.CreateBackupOrTrack(ggmPath);
                File.Copy(outputFile, ggmPath, true);
                File.Delete(outputFile);

                Logger.Log(LogLevel.Info, $"Successfully added {addedCount} new resource path(s) to globalgamemanagers");
            } catch (Exception ex) {
                Logger.Log(LogLevel.Error, $"Error writing resource paths: {ex.Message}");
                ErrorHandler.Handle("Failed to write resource paths to globalgamemanagers", ex);
            }
        }

        private static void ProcessModAssetFolder(AssetsManager manager, FileInstance assetsFileInstance,
            string baseFolder, string relativePath) {

            var currentFolder = Path.Combine(baseFolder, relativePath);

            foreach (var file in Directory.GetFiles(currentFolder)) {
                var assetName = Path.GetFileNameWithoutExtension(file);
                var assetData = File.ReadAllBytes(file);
                var extension = Path.GetExtension(file).ToLower();

                var resourcePath = relativePath;
                if (!string.IsNullOrEmpty(resourcePath)) {
                    resourcePath = resourcePath.Replace('\\', '/');
                    if (!resourcePath.EndsWith('/'))
                        resourcePath += "/";
                }

                resourcePath += assetName;

                Logger.Log(LogLevel.Info, $"Processing mod asset: {resourcePath}");

                var handler = AssetHandlers.FirstOrDefault(h => h.Extensions.Contains(extension));
                if (handler == null) {
                    Logger.Log(LogLevel.Info, $"Unsupported asset type for file: {file}");
                    continue;
                }

                if (!ReplaceAsset(manager, assetsFileInstance, assetName, assetData, resourcePath, handler))
                    Logger.Log(LogLevel.Error, $"Failed to replace asset: {resourcePath}");
            }

            foreach (var dir in Directory.GetDirectories(currentFolder))
                ProcessModAssetFolder(manager, assetsFileInstance, baseFolder,
                    Path.Combine(relativePath, Path.GetFileName(dir)));
        }

        private static bool ReplaceAsset(AssetsManager manager, FileInstance assetsFileInstance, string assetName,
            byte[] assetData, string resourcePath, AssetTypeHandlerBase handler) {
            try {
                var normalizedResourcePath = resourcePath.Replace('\\', '/');
                Logger.Log(LogLevel.Info, $"Looking for {handler.ClassId} with path '{normalizedResourcePath}'");

                if (ResourcePaths.Count > 0) {
                    foreach (var assetInfo in assetsFileInstance.file.GetAssetsOfType(handler.ClassId)) {
                        var matchingEntry = ResourcePaths.FirstOrDefault(e => 
                            e.pathId == assetInfo.PathId && 
                            string.Equals(e.path, normalizedResourcePath, StringComparison.OrdinalIgnoreCase));
                        
                        if (matchingEntry != default) {
                            Logger.Log(LogLevel.Info, $"Found exact path match: {matchingEntry.path}");
                            return handler.Replace(manager, assetsFileInstance, assetInfo, assetData);
                        }
                    }
                }

                Logger.Log(LogLevel.Info, $"Asset not found, creating new asset: {normalizedResourcePath}");
                return CreateNewAsset(manager, assetsFileInstance, assetName, assetData, normalizedResourcePath,
                    handler);
            } catch (Exception ex) {
                ErrorHandler.Handle($"Error replacing asset: {ex.Message}", ex);
                return false;
            }
        }

        private static bool CreateNewAsset(AssetsManager manager, FileInstance assetsFileInstance, string assetName,
            byte[] assetData, string resourcePath, AssetTypeHandlerBase handler) {
            try {
                var afile = assetsFileInstance.file;

                var nextPathId = afile.Metadata.AssetInfos.Select(info => info.PathId).Max() + 1;

                var newBaseField = manager.CreateValueBaseField(assetsFileInstance, (int)handler.ClassId);

                newBaseField["m_Name"].AsString = assetName;

                var newInfo = AssetFileInfo.Create(
                    afile,
                    nextPathId,
                    (int)handler.ClassId,
                    scriptIndex: ushort.MaxValue,
                    classDatabase: manager.ClassDatabase
                );

                if (newInfo == null) {
                    Logger.Log(LogLevel.Error, $"Failed to create AssetFileInfo for type {handler.ClassId}");
                    return false;
                }

                newInfo.SetNewData(newBaseField);

                if (!handler.Replace(manager, assetsFileInstance, newInfo, assetData)) {
                    Logger.Log(LogLevel.Error, $"Handler failed to create asset: {resourcePath}");
                    return false;
                }

                afile.Metadata.AddAssetInfo(newInfo);
                ResourcePaths.Add((nextPathId, resourcePath));

                Logger.Log(LogLevel.Info, $"Successfully created new asset: {resourcePath}");
                return true;
            } catch (Exception ex) {
                ErrorHandler.Handle($"Error creating new asset: {ex.Message}", ex);
                return false;
            }
        }

        private static void ReadResourcePaths(string gamePath) {
            try {
                var am = new AssetsManager();
                am.LoadClassPackage("Resources/classdata.tpk");

                var ggmPath = Path.Combine(SettingsHolder.SelectedInstall!.FullAssetsPath, "globalgamemanagers");
                if (!File.Exists(ggmPath)) {
                    Logger.Log(LogLevel.Error, $"Could not find globalgamemanagers at {ggmPath}");
                    return;
                }

                var ggm = am.LoadAssetsFile(ggmPath);
                am.LoadClassDatabaseFromPackage(ggm.file.Metadata.UnityVersion);

                var resourceManagerAssets = ggm.file.GetAssetsOfType(AssetClassID.ResourceManager);
                if (resourceManagerAssets.Count == 0) {
                    Logger.Log(LogLevel.Error, $"No ResourceManager asset found in globalgamemanagers");
                    return;
                }

                var rsrcInfo = resourceManagerAssets[0];
                var rsrcBf = am.GetBaseField(ggm, rsrcInfo);

                var m_Container = rsrcBf["m_Container.Array"];

                foreach (var data in m_Container.Children) {
                    var name = data[0].AsString;
                    var pathId = data[1]["m_PathID"].AsLong;

                    ResourcePaths.Add((pathId, name));
                }

                ggm.file.Close();
                am.UnloadAll();
            } catch (Exception ex) {
                Logger.Log(LogLevel.Error, $"Error reading resource paths: {ex.Message}");
                ErrorHandler.Handle("Failed to read asset paths from globalgamemanagers", ex);
            }
        }
    }
}