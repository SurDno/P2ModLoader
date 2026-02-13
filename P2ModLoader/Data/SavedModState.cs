namespace P2ModLoader.Data;

public class SavedModState(string modName, bool isEnabled, int loadOrder) {
	public string ModName { get; set; } = modName;
	public bool IsEnabled { get; set; } = isEnabled;
	public int LoadOrder { get; set; } = loadOrder;
	public Dictionary<string, object?> OptionValues { get; set; } = new();
}