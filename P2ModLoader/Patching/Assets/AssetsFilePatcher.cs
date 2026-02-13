using AssetsTools.NET;
using AssetsTools.NET.Extra;
using P2ModLoader.Helper;
using P2ModLoader.Logging;
using P2ModLoader.Patching.Backups;
using FileInstance = AssetsTools.NET.Extra.AssetsFileInstance;

namespace P2ModLoader.Patching.Assets;

public static class AssetsFilePatcher {
    private static readonly List<(long pathId, string path)> ResourcePaths = [];
    private static readonly List<AssetTypeHandlerBase> AssetHandlers =
        [new TextAssetHandler(), new AudioAssetHandler(), new TextureAssetHandler()];
    
    private static HashSet<(long pathId, string normalizedPath)>? _resourcePathLookup;
    private static Dictionary<AssetClassID, List<AssetFileInfo>>? _assetsByType;

    private static readonly AssetsManager SharedManager;
    
    static AssetsFilePatcher() {
        SharedManager = new AssetsManager();
        SharedManager.LoadClassPackage("Resources/classdata.tpk");
        Logger.Log(LogLevel.Debug, $"Loaded classdata.tpk into shared AssetsManager");
    }
    
    public static bool PatchAssetsFile(string assetsFilePath, List<string> modAssetsFolders) {
        var outputFile = assetsFilePath + ".new";
        ResourcePaths.Clear();
        BackupManager.CreateBackupOrTrack(assetsFilePath);
        
        try {
            var assetsFileInstance = SharedManager.LoadAssetsFile(assetsFilePath);
            SharedManager.LoadClassDatabaseFromPackage(assetsFileInstance.file.Metadata.UnityVersion);

            if (Path.GetFileName(assetsFilePath).Equals("resources.assets", StringComparison.OrdinalIgnoreCase))
                ReadResourcePaths(Path.GetDirectoryName(Path.GetDirectoryName(assetsFilePath))!);

            BuildLookupCaches(assetsFileInstance);

            int initialPathCount = ResourcePaths.Count;
            
            foreach (var modAssetsFolder in modAssetsFolders) 
                ProcessModAssetFolder(assetsFileInstance, modAssetsFolder, "");
            
            var resourcePathsModified = ResourcePaths.Count > initialPathCount;

            using (var writer = new AssetsFileWriter(outputFile))
                assetsFileInstance.file.Write(writer, -1);
            assetsFileInstance.file.Close();
            File.Copy(outputFile, assetsFilePath, true);

            if (resourcePathsModified)
                WriteResourcePaths(Path.GetDirectoryName(Path.GetDirectoryName(assetsFilePath))!);
            
            Logger.Log(LogLevel.Info, $"Successfully patched {assetsFilePath}");
            SharedManager.UnloadAll();
            return true;
        } catch (Exception ex) {
            ErrorHandler.Handle("Error patching assets file", ex);
            return false;
        } finally {
            _resourcePathLookup = null;
            _assetsByType = null;
            if (File.Exists(outputFile))
                File.Delete(outputFile);
        }
    }

    private static void BuildLookupCaches(FileInstance assetsFileInstance) {
        _resourcePathLookup = ResourcePaths
            .Select(rp => (rp.pathId, rp.path.ToLowerInvariant()))
            .ToHashSet();

        _assetsByType = new Dictionary<AssetClassID, List<AssetFileInfo>>();
        foreach (var handler in AssetHandlers) {
            _assetsByType[handler.ClassId] = assetsFileInstance.file
                .GetAssetsOfType(handler.ClassId)
                .ToList();
        }
    }

    private static void WriteResourcePaths(string gamePath) {
        try {
            var ggmPath = Path.Combine(SettingsHolder.SelectedInstall!.FullAssetsPath, "globalgamemanagers");
            var ggm = SharedManager.LoadAssetsFile(ggmPath);

            var resourceManagerAssets = ggm.file.GetAssetsOfType(AssetClassID.ResourceManager);
            
            var index = ggm.file.Metadata.Externals.FindIndex(e => e.PathName == "resources.assets");
            if (index == -1) {
                var newExternal = new AssetsFileExternal { PathName = "resources.assets" };
                ggm.file.Metadata.Externals.Add(newExternal);
                index = ggm.file.Metadata.Externals.Count - 1;
            }
            var fileId = index + 1; 

            var rsrcInfo = resourceManagerAssets[0];
            var rsrcBf = SharedManager.GetBaseField(ggm, rsrcInfo);

            var m_Container = rsrcBf["m_Container.Array"];

            var existingEntries = new HashSet<(long pathId, string path)>();
            foreach (var data in m_Container.Children) {
                var pathId = data[1]["m_PathID"].AsLong;
                var path = data[0].AsString;
                existingEntries.Add((pathId, path));
            }

            var addedCount = 0;
            foreach (var entry in ResourcePaths) {
                if (existingEntries.Contains(entry)) continue;
                var pair = ValueBuilder.DefaultValueFieldFromArrayTemplate(m_Container);
                pair[0].AsString = entry.path;
                pair[1]["m_FileID"].AsInt = fileId;
                pair[1]["m_PathID"].AsLong = entry.pathId;
                m_Container.Children.Add(pair);
                addedCount++;
                Logger.Log(LogLevel.Info, $"Added new resource path: {entry.path} -> PathID {entry.pathId}");
            }

            if (addedCount == 0) {
                Logger.Log(LogLevel.Info, $"No new resource paths to add");
                return;
            }

            rsrcInfo.SetNewData(rsrcBf);

            var outputFile = ggmPath + ".new";
            using (var writer = new AssetsFileWriter(outputFile)) {
                ggm.file.Write(writer, -1);
            }
            ggm.file.Close();

            BackupManager.CreateBackupOrTrack(ggmPath);
            File.Copy(outputFile, ggmPath, true);
            File.Delete(outputFile);

            Logger.Log(LogLevel.Info, $"Successfully added {addedCount} new resource path(s) to globalgamemanagers");
        } catch (Exception ex) {
            Logger.Log(LogLevel.Error, $"Error writing resource paths: {ex.Message}");
            ErrorHandler.Handle("Failed to write resource paths to globalgamemanagers", ex);
        }
    }

    private static void ProcessModAssetFolder(FileInstance assetsFileInstance,
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

            if (!ReplaceAsset(assetsFileInstance, assetName, assetData, resourcePath, handler))
                Logger.Log(LogLevel.Error, $"Failed to replace asset: {resourcePath}");
        }

        foreach (var dir in Directory.GetDirectories(currentFolder))
            ProcessModAssetFolder(assetsFileInstance, baseFolder, Path.Combine(relativePath, Path.GetFileName(dir)));
    }

    private static bool ReplaceAsset(FileInstance assetsFileInstance, string assetName,
        byte[] assetData, string resourcePath, AssetTypeHandlerBase handler) {
        try {
            var normalizedResourcePath = resourcePath.Replace('\\', '/');
            Logger.Log(LogLevel.Info, $"Looking for {handler.ClassId} with path '{normalizedResourcePath}'");

            if (_assetsByType != null && 
                _assetsByType.TryGetValue(handler.ClassId, out var assetList) &&
                _resourcePathLookup != null) {
                
                var normalizedLower = normalizedResourcePath.ToLowerInvariant();
                
                foreach (var assetInfo in assetList) {
                    if (_resourcePathLookup.Contains((assetInfo.PathId, normalizedLower))) {
                        Logger.Log(LogLevel.Info, $"Found exact path match for PathID {assetInfo.PathId}");
                        return handler.Replace(SharedManager, assetsFileInstance, assetInfo, assetData);
                    }
                }
            }

            Logger.Log(LogLevel.Info, $"Asset not found, creating new asset: {normalizedResourcePath}");
            return CreateNewAsset(assetsFileInstance, assetName, assetData, normalizedResourcePath, handler);
        } catch (Exception ex) {
            ErrorHandler.Handle($"Error replacing asset: {ex.Message}", ex);
            return false;
        }
    }

    private static bool CreateNewAsset(FileInstance assetsFileInstance, string assetName,
        byte[] assetData, string resourcePath, AssetTypeHandlerBase handler) {
        try {
            var afile = assetsFileInstance.file;

            var nextPathId = afile.Metadata.AssetInfos.Select(info => info.PathId).Max() + 1;

            var newBaseField = SharedManager.CreateValueBaseField(assetsFileInstance, (int)handler.ClassId);

            newBaseField["m_Name"].AsString = assetName;

            var newInfo = AssetFileInfo.Create(
                afile,
                nextPathId,
                (int)handler.ClassId,
                scriptIndex: ushort.MaxValue,
                classDatabase: SharedManager.ClassDatabase
            );

            if (newInfo == null) {
                Logger.Log(LogLevel.Error, $"Failed to create AssetFileInfo for type {handler.ClassId}");
                return false;
            }

            newInfo.SetNewData(newBaseField);

            if (!handler.Replace(SharedManager, assetsFileInstance, newInfo, assetData)) {
                Logger.Log(LogLevel.Error, $"Handler failed to create asset: {resourcePath}");
                return false;
            }

            afile.Metadata.AddAssetInfo(newInfo);
            ResourcePaths.Add((nextPathId, resourcePath));

            _resourcePathLookup?.Add((nextPathId, resourcePath.ToLowerInvariant()));
            if (_assetsByType != null && _assetsByType.TryGetValue(handler.ClassId, out var list)) {
                list.Add(newInfo);
            }

            Logger.Log(LogLevel.Info, $"Successfully created new asset: {resourcePath}");
            return true;
        } catch (Exception ex) {
            ErrorHandler.Handle($"Error creating new asset: {ex.Message}", ex);
            return false;
        }
    }

    private static void ReadResourcePaths(string gamePath) {
        try {
            var ggmPath = Path.Combine(SettingsHolder.SelectedInstall!.FullAssetsPath, "globalgamemanagers");

            var ggm = SharedManager.LoadAssetsFile(ggmPath);

            var resourceManager = ggm.file.GetAssetsOfType(AssetClassID.ResourceManager)[0];
            foreach (var data in SharedManager.GetBaseField(ggm, resourceManager)["m_Container.Array"].Children) 
                ResourcePaths.Add((data[1]["m_PathID"].AsLong, data[0].AsString));
            
        } catch (Exception ex) {
            Logger.Log(LogLevel.Error, $"Error reading resource paths: {ex.Message}");
            ErrorHandler.Handle("Failed to read asset paths from globalgamemanagers", ex);
        }
    }
}