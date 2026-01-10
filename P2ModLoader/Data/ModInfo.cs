using P2ModLoader.Helper;

namespace P2ModLoader.Data;

public class ModInfo {
	public string Name { get; private set; } = "???";
	public string Description { get; private set; } = "No description.";
	public string Author { get; private set; } = "???";
	public string Version { get; private set; } = "???";
	public string Url { get; private set; } = string.Empty;
	public List<string> Requirements { get; private set; } = [];
	public List<string> LoadAfterMods { get; private set; } = []; 
	public List<string> LoadFirst { get; private set; } = [];
	public string MinLoaderVersion { get; private set; } = string.Empty;
	public List<Game> Games { get; private set; } = [Game.Pathologic2];

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
				case "games":
					info.Games = ParseGames(value);
					break;
				default:
					ErrorHandler.Handle($"Mod located at {Path.GetDirectoryName(filePath)} has unsupported setting in" +
					                    $" its ModInfo.ltx: {key}. Either there is a mistake in the file, or an" +
					                    $" update to the mod loader is required to properly parse that data.", null);
					break;
			}
		}

		return info;
	}

	private static List<string> GetList(string value) =>
		value.Split(',').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToList();
	
	private static List<Game> ParseGames(string value) {
		var gameNames = GetList(value);
		var games = new List<Game>();
		
		foreach (var gameName in gameNames) {
			Game? parsed = gameName.ToLower().Replace(" ", "") switch {
				"marblenest" or "mn" => Game.MarbleNest,
				"pathologic2alpha" or "p2a" => Game.Pathologic2Alpha,
				"pathologic2demo" or "p2d" => Game.Pathologic2Demo,
				"pathologic2" or "p2" => Game.Pathologic2,
				"pathologic3quarantine" or "p3q" or "quarantine" => Game.Pathologic3Quarantine,
				"pathologic3demo" or "p3d" => Game.Pathologic3Demo,
				"pathologic3" or "p3" => Game.Pathologic3,
				_ => null
			};
			
			if (parsed.HasValue && !games.Contains(parsed.Value))
				games.Add(parsed.Value);
		}
		
		return games.Count > 0 ? games : [Game.Pathologic2];
	}
}