using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using P2ModLoader.Helper;
using P2ModLoader.Logging;
using FileInstance = AssetsTools.NET.Extra.AssetsFileInstance;

namespace P2ModLoader.Patching.Assets {
    public sealed class TextureAssetHandler() : AssetTypeHandlerBase(AssetClassID.Texture2D, ".png", ".jpg", ".jpeg") {
        public override bool Replace(AssetsManager am, FileInstance fileInst, AssetFileInfo assetInfo, byte[] data) {
            try {
                var baseField = am.GetBaseField(fileInst, assetInfo);
                var textureFile = TextureFile.ReadTextureFile(baseField);
                
                Logger.Log(LogLevel.Info, $"Original texture format: {(TextureFormat)textureFile.m_TextureFormat}, " +
                               $"dimensions: {textureFile.m_Width}x{textureFile.m_Height}");
                
                Bitmap bitmap;
                using (var ms = new MemoryStream(data)) 
                    bitmap = new Bitmap(ms);
                
                bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
                
                Logger.Log(LogLevel.Info, $"Mod texture dimensions: {bitmap.Width}x{bitmap.Height}");
                
                var rgbaData = BitmapToRGBA32(bitmap);
                
                textureFile.m_Width = bitmap.Width;
                textureFile.m_Height = bitmap.Height;
                textureFile.m_TextureFormat = (int)TextureFormat.RGBA32;
                textureFile.m_CompleteImageSize = rgbaData.Length;
                textureFile.pictureData = rgbaData;
                textureFile.m_MipCount = 1;
                textureFile.m_TextureDimension = 2;
                textureFile.m_TextureSettings.m_FilterMode = 1;
                textureFile.m_TextureSettings.m_WrapU = 1;
                textureFile.m_TextureSettings.m_WrapV = 1;
                textureFile.m_TextureSettings.m_WrapW = 1;
                textureFile.m_TextureSettings.m_Aniso = 1;
                textureFile.m_ImageCount = 1;
                textureFile.m_IsReadable = true;
                
                bitmap.Dispose();
                
                textureFile.m_StreamData = new TextureFile.StreamingInfo {
                    offset = 0,
                    size = 0,
                    path = string.Empty
                };
                
                textureFile.WriteTo(baseField);
                
                assetInfo.Replacer = new ContentReplacerFromBuffer(baseField.WriteToByteArray());
                
                Logger.Log(LogLevel.Info, $"Successfully replaced texture with RGBA32 format at {textureFile.m_Width}x{textureFile.m_Height}");
                return true;
            } catch (Exception ex) {
                ErrorHandler.Handle("Error replacing texture asset", ex);
                return false;
            }
        }

        private byte[] BitmapToRGBA32(Bitmap bitmap) {
            var rgbaData = new byte[bitmap.Width * bitmap.Height * 4];
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            try {
                Marshal.Copy(bitmapData.Scan0, rgbaData, 0, rgbaData.Length);

                for (var i = 0; i < rgbaData.Length; i += 4) {
                    var b = rgbaData[i];
                    var g = rgbaData[i + 1];
                    var r = rgbaData[i + 2];
                    var a = rgbaData[i + 3];

                    rgbaData[i] = r;  
                    rgbaData[i + 1] = g; 
                    rgbaData[i + 2] = b; 
                    rgbaData[i + 3] = a;
                }
            } finally {
                bitmap.UnlockBits(bitmapData);
            }

            return rgbaData;
        }
    }
}