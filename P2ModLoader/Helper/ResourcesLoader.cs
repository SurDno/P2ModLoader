using System.Reflection;

namespace P2ModLoader.Helper {
	public static class ResourcesLoader {
		private static readonly Assembly ResourcesAssembly = Assembly.GetExecutingAssembly();

		private static readonly Dictionary<string, Image> ImageCache = new();
		private static readonly Dictionary<string, Icon> IconCache = new();

		public static Image? LoadImage(string imageName, string extension = "jpg") {
			var resourceName = $"{ResourcesAssembly.GetName().Name}.Resources.{imageName}.{extension}";

			if (ImageCache.TryGetValue(resourceName, out var cached)) return cached;
			
			using var stream = ResourcesAssembly.GetManifestResourceStream(resourceName);
			if (stream == null) return null;
			
			using var temp = Image.FromStream(stream);
			var image = new Bitmap(temp); 
			ImageCache[resourceName] = image;
			return image;
		}
	}
}