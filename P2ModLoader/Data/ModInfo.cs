namespace P2ModLoader.Data;

public class ModInfo {
	public string Name { get; private set; } = string.Empty;
	public string Description { get; private set; } = string.Empty;
	public string Author { get; private set; } = string.Empty;
	public string Version { get; private set; } = string.Empty;
	public string Url { get; private set; } = string.Empty;
	public List<string> Requirements { get; private set; } = [];
	public List<string> LoadAfterMods { get; private set; } = []; 
	public List<string> LoadFirst { get; private set; } = [];
	public string MinLoaderVersion { get; private set; }

	public static ModInfo FromFile(string filePath) {
		var info = new ModInfo();
        
		if (!File.Exists(filePath)) return info;

		foreach (var line in File.ReadAllLines(filePath)) {
			var indexOfEquals = line.IndexOf('=');
			if (indexOfEquals == -1) continue;

			var key = line[..indexOfEquals].Trim();
			var value = line[(indexOfEquals + 1)..].Trim();

			switch (key.ToLower()) {
				case "name":
					info.Name = value;
					break;
				case "description":
					info.Description = value;
					break;
				case "author":
					info.Author = value;
					break;
				case "version":
					info.Version = value;
					break;
				case "url":
					info.Url = value;
					break;
				case "requirements":
					info.Requirements = GetList(value);
					break;
				case "goes_after":
					info.LoadAfterMods = GetList(value);
					break;
				case "load_first":
					info.LoadFirst = GetList(value).Select(x => x.EndsWith(".cs") ? x : x + ".cs").ToList();
					break;
				case "min_loader_version":
					info.MinLoaderVersion = value;
					break;
			}
		}

		return info;
	}

	private static List<string> GetList(string value) =>
		value.Split(',').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList();
}