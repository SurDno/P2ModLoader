using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using P2ModLoader.Helper;
using P2ModLoader.Logging;
using FileInstance = AssetsTools.NET.Extra.AssetsFileInstance;

namespace P2ModLoader.Patching.Assets {
    public class TextureAssetHandler() : AssetTypeHandlerBase(AssetClassID.Texture2D, ".png", ".jpg", ".jpeg") {
        public override bool Replace(AssetsManager am, FileInstance fileInst, AssetFileInfo assetInfo, byte[] data) {
            try {
                var baseField = am.GetBaseField(fileInst, assetInfo);
                var textureFile = TextureFile.ReadTextureFile(baseField);
                
                Logger.Log(LogLevel.Info, $"Original texture format: {(TextureFormat)textureFile.m_TextureFormat}, " +
                               $"dimensions: {textureFile.m_Width}x{textureFile.m_Height}");
                
                Bitmap bitmap;
                using (var ms = new MemoryStream(data)) 
                    bitmap = new Bitmap(ms);
                
                Logger.Log(LogLevel.Info, $"Mod texture dimensions: {bitmap.Width}x{bitmap.Height}");
                
                if (bitmap.Width != textureFile.m_Width || bitmap.Height != textureFile.m_Height) {
                    Logger.Log(LogLevel.Info, $"Resizing image to match original dimensions: " +
                                              $"{textureFile.m_Width}x{textureFile.m_Height}");
                    var resized = new Bitmap(textureFile.m_Width, textureFile.m_Height);
                    using (var g = Graphics.FromImage(resized)) {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(bitmap, 0, 0, textureFile.m_Width, textureFile.m_Height);
                    }
                    bitmap.Dispose();
                    bitmap = resized;
                }
                
                var bgraData = BitmapToBGRA(bitmap);
                bitmap.Dispose();
                
                textureFile.m_TextureFormat = (int)TextureFormat.RGBA32;
                
                try {
                    textureFile.SetTextureData(bgraData, textureFile.m_Width, textureFile.m_Height);
                    
                    textureFile.WriteTo(baseField);
                    
                    assetInfo.Replacer = new ContentReplacerFromBuffer(baseField.WriteToByteArray());
                    
                    Logger.Log(LogLevel.Info, $"Successfully replaced texture with RGBA32 format");
                    return true;
                } catch (Exception ex) {
                    Logger.Log(LogLevel.Error, $"Failed to set texture data: {ex.Message}");
                    return false;
                }
            } catch (Exception ex) {
                ErrorHandler.Handle("Error replacing texture asset", ex);
                return false;
            }
        }
        
        private byte[] BitmapToBGRA(Bitmap bitmap) {
            var bgraData = new byte[bitmap.Width * bitmap.Height * 4];
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            try {
                Marshal.Copy(bitmapData.Scan0, bgraData, 0, bgraData.Length);

                for (var i = 0; i < bgraData.Length; i += 4) {
                    var a = bgraData[i];
                    var r = bgraData[i + 1];
                    var g = bgraData[i + 2];
                    var b = bgraData[i + 3];

                    bgraData[i] = b;
                    bgraData[i + 1] = g; 
                    bgraData[i + 2] = r; 
                    bgraData[i + 3] = a;
                }
            } finally {
                bitmap.UnlockBits(bitmapData);
            }

            return bgraData;
        }
    }
}