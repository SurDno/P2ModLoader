using AssetsTools.NET;
using AssetsTools.NET.Extra;
using P2ModLoader.Helper;

namespace P2ModLoader.Patching.Assets;

public static class AssetsFilePatcher {
    public static bool PatchAssetsFile(string assetsFilePath, string modAssetsFolder) {
        var manager = new AssetsManager();

        var newFile = assetsFilePath + ".temp";
        File.Copy(assetsFilePath, newFile, true);

        try {
            manager.LoadClassPackage("Resources/classdata.tpk");

            Logger.LogInfo($"Class package loaded.");

            using var readFs = new FileStream(assetsFilePath, FileMode.Open, FileAccess.Read);
            var assetsFileInstance = manager.LoadAssetsFile(readFs, assetsFilePath);
            var assetsFile = assetsFileInstance.file;

            manager.LoadClassDatabaseFromPackage(assetsFile.Metadata.UnityVersion);

            var modAssetFiles = Directory.GetFiles(modAssetsFolder, "*.*", SearchOption.AllDirectories);
            foreach (var modAssetFile in modAssetFiles) {
                var name = Path.GetFileNameWithoutExtension(modAssetFile);

                switch (Path.GetExtension(modAssetFile).ToLower()) {
                    case ".bytes": 
                        if (!ReplaceTextAsset(manager, assetsFileInstance, name, File.ReadAllBytes(modAssetFile)))
                            return false;
                        break;
                    default:
                        Logger.LogInfo($"Unsupported asset type for file: {modAssetFile}");
                        break;
                }
            }


            using var writeFs = new FileStream(newFile, FileMode.Create, FileAccess.Write);
            using var writer = new AssetsFileWriter(writeFs);
            assetsFileInstance.file.Write(writer, -1);
            readFs.Close();
            writeFs.Close();
            File.Copy(newFile, assetsFilePath, true);
            return true;
        } catch (Exception ex) {
            ErrorHandler.Handle("Error patching assets file", ex);
            return false;
        } finally {
            File.Delete(newFile);
            manager.UnloadAll();
        }
    }
    
    private static bool ReplaceTextAsset(AssetsManager manager, AssetsFileInstance assetsFileInstance, 
        string assetName, byte[] assetData) {
        try {
            var textAssets = assetsFileInstance.file.GetAssetsOfType(AssetClassID.TextAsset);

            foreach (var assetInfo in textAssets) {
                var baseField = manager.GetBaseField(assetsFileInstance, assetInfo);
                if (baseField["m_Name"].AsString != assetName) continue;
                baseField["m_Script"].AsByteArray = assetData;
                assetInfo.Replacer = new ContentReplacerFromBuffer(baseField.WriteToByteArray());
                Logger.LogInfo($"Successfully replaced text asset {assetName}");
                return true;
            }

            ErrorHandler.Handle("Text asset with name {assetName} not found.", null);
            return false;
        } catch (Exception ex) {
            ErrorHandler.Handle($"Error replacing text asset", ex);
            return false;
        }
    }
}