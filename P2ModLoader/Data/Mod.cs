namespace P2ModLoader.Data;

public class Mod {
	private string? _dependencyError = string.Empty;
	
	public ModInfo Info { get; }
	public string FolderPath { get; }
	public string FolderName => new DirectoryInfo(FolderPath.TrimEnd('/')).Name;
	public bool IsEnabled { get; set; }
	public int LoadOrder { get; set; }
	public ModOptions? Options { get; private set; }
	public Dictionary<string, object?> OptionValues { get; set; } = new();

	public string? DependencyError {
		set => _dependencyError = value;
		get => string.IsNullOrEmpty(_dependencyError) ? "" : $"\r\nDependency error: {_dependencyError}";
	}

	public Mod(string folderPath) {
		FolderPath = folderPath;
		var infoPath = Path.Combine(folderPath, "ModInfo.ltx");
		Info = ModInfo.FromFile(infoPath);
    
		var optionsPath = Path.Combine(folderPath, "ModOptions.json");
		if (!File.Exists(optionsPath)) return;
		Options = ModOptions.FromFile(optionsPath); 
		InitializeOptionValues();
	}

	private void InitializeOptionValues() {
		if (Options == null) return;
    
		foreach (var category in Options.Categories) {
			foreach (var option in category.Options) {
				if (!OptionValues.TryGetValue(option.Name, out var value)) {
					OptionValues[option.Name] = option.DefaultValue;
					option.CurrentValue = option.DefaultValue;
				} else {
					option.CurrentValue = value;
				}
			}
		}
	}
	
	public bool HasOptions => Options?.Categories.Any(c => c.Options.Count > 0) ?? false;
	
	public bool IsCompatibleWith(Install install) => Info.Games.Contains(install.Game);

	public string GetModificationTypes() { 	
		var modTypes = new List<string>();

		var p2 = Info.Games.Contains(Game.Pathologic2);
		
		AddToListIfDefinitionExists(modTypes, "dll", ".dll files", FolderPath);
		AddToListIfDefinitionExists(modTypes, "cs", "code", FolderPath);
		if (p2) {
			AddToListIfDefinitionExists(modTypes, "xml", "templates", Path.Combine(FolderPath, "Data", "Templates"));
			AddToListIfDefinitionExists(modTypes, "gz", "templates", Path.Combine(FolderPath, "Data", "Templates"));
			AddToListIfDefinitionExists(modTypes, "xml", "VM", Path.Combine(FolderPath, "Data", "VirtualMachine"));
		}
		AddToListIfDefinitionExists(modTypes, "bytes", "assets (text)", Path.Combine(FolderPath, "Pathologic_Data"));
		AddToListIfDefinitionExists(modTypes, "txt", "assets (text)", Path.Combine(FolderPath, "Pathologic_Data"));
		AddToListIfDefinitionExists(modTypes, "png", "assets (textures)", Path.Combine(FolderPath, "Pathologic_Data"));
		AddToListIfDefinitionExists(modTypes, "bytes", "assets (text)", Path.Combine(FolderPath, "Pathologic3_Data"));
		AddToListIfDefinitionExists(modTypes, "txt", "assets (text)", Path.Combine(FolderPath, "Pathologic3_Data"));
		AddToListIfDefinitionExists(modTypes, "png", "assets (textures)", Path.Combine(FolderPath, "Pathologic3_Data"));
    
		return modTypes.Count > 0 ? $"Modifies: {string.Join(", ", modTypes)}" : "No modifications detected";
	}

	private static void AddToListIfDefinitionExists(List<string> modTypes, string ext, string result, string path) { 	
		if (Directory.Exists(path) && Directory.GetFiles(path, $"*.{ext}", SearchOption.AllDirectories).Length != 0) 
			modTypes.Add(result);
	}
}