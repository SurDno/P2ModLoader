using AssetsTools.NET;
using AssetsTools.NET.Extra;
using FileInstance = AssetsTools.NET.Extra.AssetsFileInstance;
namespace P2ModLoader.Patching.Assets;

public abstract class AssetTypeHandlerBase(AssetClassID classId, params string[] extensions) {
	public AssetClassID ClassId { get; } = classId;
	public string[] Extensions { get; } = extensions;
	
	public abstract bool Replace(AssetsManager am, FileInstance fileInst, AssetFileInfo assetInfo, byte[] data);
}