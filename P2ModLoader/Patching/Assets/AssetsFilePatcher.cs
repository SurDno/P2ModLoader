using AssetsTools.NET;
using AssetsTools.NET.Extra;
using P2ModLoader.Helper;
using P2ModLoader.Logging;
using FileInstance = AssetsTools.NET.Extra.AssetsFileInstance;

namespace P2ModLoader.Patching.Assets {
    public static class AssetsFilePatcher {
        private static readonly Dictionary<long, string> _resourcePaths = new();

        private static readonly List<AssetTypeHandlerBase> _assetHandlers =
            [new TextAssetHandler(), new AudioAssetHandler(), new TextureAssetHandler()];

        public static bool PatchAssetsFile(string assetsFilePath, string modAssetsFolder) {
            AssetsManager? manager = null;
            FileInstance? assetsFileInstance = null;

            var outputFile = assetsFilePath + ".new";

            try {
                manager = new AssetsManager();

                assetsFileInstance = manager.LoadAssetsFile(assetsFilePath);
                manager.LoadClassDatabase("Resources/cldb_2018.4.6f1.dat");
                if (Path.GetFileName(assetsFilePath).Equals("resources.assets", StringComparison.OrdinalIgnoreCase))
                    ReadResourcePaths(Path.GetDirectoryName(Path.GetDirectoryName(assetsFilePath)));
            
                ProcessModAssetFolder(manager, assetsFileInstance, modAssetsFolder, "");

                using (var writer = new AssetsFileWriter(outputFile)) 
                    assetsFileInstance.file.Write(writer, -1);
                assetsFileInstance.file.Close();
                assetsFileInstance = null;

                BackupManager.CreateBackup(assetsFilePath);
                File.Copy(outputFile, assetsFilePath, true);
                
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

                var handler = _assetHandlers.FirstOrDefault(h => h.Extensions.Contains(extension));
                if (handler == null) {
                    Logger.Log(LogLevel.Info, $"Unsupported asset type for file: {file}");
                    continue;
                }

                if (!ReplaceAsset(manager, assetsFileInstance, assetName, assetData, resourcePath, handler))
                    Logger.Log(LogLevel.Error, $"Failed to replace asset: {resourcePath}");
            }
            
            foreach (var dir in Directory.GetDirectories(currentFolder))
                ProcessModAssetFolder(manager, assetsFileInstance, baseFolder, Path.Combine(relativePath, Path.GetFileName(dir)));
        }

        private static bool ReplaceAsset(AssetsManager manager, FileInstance assetsFileInstance, string assetName, 
            byte[] assetData, string resourcePath, AssetTypeHandlerBase handler) {
            try {
                var normalizedResourcePath = resourcePath.Replace('\\', '/');
                Logger.Log(LogLevel.Info, $"Looking for {handler.ClassId} with path '{normalizedResourcePath}'");
            
                if (_resourcePaths.Count > 0) {
                    foreach (var assetInfo in assetsFileInstance.file.GetAssetsOfType(handler.ClassId)) {
                        if (!_resourcePaths.TryGetValue(assetInfo.PathId, out var path)) continue;
                        if (!string.Equals(path, normalizedResourcePath, StringComparison.OrdinalIgnoreCase)) continue;
                        Logger.Log(LogLevel.Info, $"Found exact path match: {path}");
                        return handler.Replace(manager, assetsFileInstance, assetInfo, assetData);
                    }
                }

                ErrorHandler.Handle($"Asset with path {resourcePath} not found.", null);
                return false;
            } catch (Exception ex) {
                ErrorHandler.Handle($"Error replacing asset: {ex.Message}", ex);
                return false;
            }
        }

        private static void ReadResourcePaths(string gamePath) {
            try {
                var am = new AssetsManager();
                am.LoadClassDatabase("Resources/cldb_2018.4.6f1.dat");
                
                var ggmPath = Path.Combine(gamePath, "Pathologic_Data", "globalgamemanagers");
                if (!File.Exists(ggmPath)) {
                    Logger.Log(LogLevel.Error, $"Could not find globalgamemanagers at {ggmPath}");
                    return;
                }
                
                var ggm = am.LoadAssetsFile(ggmPath);
                
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
                    
                    _resourcePaths[pathId] = name;
                }
                
                ggm.file.Close();
                am.UnloadAllAssetsFiles();
            } catch (Exception ex) {
                Logger.Log(LogLevel.Error, $"Error reading resource paths: {ex.Message}");
                ErrorHandler.Handle("Failed to read asset paths from globalgamemanagers", ex);
            }
        }
    }
}