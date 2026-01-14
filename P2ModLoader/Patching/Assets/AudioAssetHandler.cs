using AssetsTools.NET;
using AssetsTools.NET.Extra;
using P2ModLoader.Helper;
using P2ModLoader.Logging;
using FileInstance = AssetsTools.NET.Extra.AssetsFileInstance;

namespace P2ModLoader.Patching.Assets;

public sealed class AudioAssetHandler() : AssetTypeHandlerBase(AssetClassID.AudioClip, ".wav", ".ogg", ".mp3") {
	public override bool Replace(AssetsManager am, FileInstance fileInst, AssetFileInfo assetInfo, byte[] data) {
		try {
			var baseField = am.GetBaseField(fileInst, assetInfo);

			var assetsDirectory = Path.GetDirectoryName(fileInst.path);
			var resourceFilename = Path.GetFileNameWithoutExtension(fileInst.path) + ".resource";
			var externalFilePath = Path.Combine(assetsDirectory, resourceFilename);

			if (File.Exists(externalFilePath)) 
				BackupManager.CreateBackupOrTrack(externalFilePath);
			

			long newOffset;
			using (var stream = new FileStream(externalFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
				       FileShare.None)) {
				newOffset = stream.Length;
				stream.Position = newOffset;
				stream.Write(data, 0, data.Length);
			}

			var resourceField = baseField["m_Resource"];
			resourceField["m_Offset"].AsULong = (ulong)newOffset;
			resourceField["m_Size"].AsUInt = (uint)data.Length;

			assetInfo.Replacer = new ContentReplacerFromBuffer(baseField.WriteToByteArray());
			Logger.Log(LogLevel.Info, $"Successfully replaced audio asset");
			return true;
		} catch (Exception ex) {
			ErrorHandler.Handle("Error replacing audio asset", ex);
			return false;
		}
	}
}