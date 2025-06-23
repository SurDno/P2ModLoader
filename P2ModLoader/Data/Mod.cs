using P2ModLoader.Logging;

namespace P2ModLoader.Data;

public class Mod {
	private string? _dependencyError = string.Empty;
	
	public ModInfo Info { get; }
	public string FolderPath { get; }
	public string FolderName => new DirectoryInfo(FolderPath.TrimEnd('/')).Name;
	public bool IsEnabled { get; set; }
	public int LoadOrder { get; set; }
	
	public string? DependencyError {
		set => _dependencyError = value;
		get => string.IsNullOrEmpty(_dependencyError) ? "" : $"\r\nDependency error: {_dependencyError}";
	}

	public Mod(string folderPath) {
		using var perf = PerformanceLogger.Log();
		FolderPath = folderPath;
		var infoPath = Path.Combine(folderPath, "ModInfo.ltx");
		Info = ModInfo.FromFile(infoPath);
		IsEnabled = false;
		LoadOrder = 0;
	}

	public string GetModificationTypes() {
		using var perf = PerformanceLogger.Log();
		var modTypes = new List<string>();
    
		AddToListIfDefinitionExists(modTypes, "dll", ".dll files", FolderPath);
		AddToListIfDefinitionExists(modTypes, "cs", "code", FolderPath);
		AddToListIfDefinitionExists(modTypes, "xml", "templates", Path.Combine(FolderPath, "Data", "Templates"));
		AddToListIfDefinitionExists(modTypes, "gz", "templates", Path.Combine(FolderPath, "Data", "Templates"));
		AddToListIfDefinitionExists(modTypes, "xml", "VM", Path.Combine(FolderPath, "Data", "VirtualMachine"));
		AddToListIfDefinitionExists(modTypes, "bytes", "assets (text)", Path.Combine(FolderPath, "Pathologic_Data"));
    
		return modTypes.Count > 0 ? $"Modifies: {string.Join(", ", modTypes)}" : "No modifications detected";
	}

	private static void AddToListIfDefinitionExists(List<string> modTypes, string ext, string result, string path) {
		using var perf = PerformanceLogger.Log();
		if (Directory.Exists(path) && Directory.GetFiles(path, $"*.{ext}", SearchOption.AllDirectories).Length != 0) 
			modTypes.Add(result);
	}
}