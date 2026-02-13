using P2ModLoader.Data;
using P2ModLoader.Logging;

namespace P2ModLoader.Helper;

public static class SettingsHolder {
	private static List<Install> _installs = [];
	private static string? _selectedInstallId;
	private static bool _allowStartupWithConflicts;
	private static bool _isPatched = true;
	private static bool _checkForUpdatesOnStartup = true;
	private static List<SavedModState> _lastKnownModState = [];
	private static Size _windowSize = new(600, 800);
	private static LogLevel _logLevel = LogLevel.Info;
	
	public static event Action? InstallPathChanged,
		InstallsChanged,
		StartupWithConflictsChanged,
		PatchStatusChanged,
		ModStateChanged,
		CheckForUpdatesOnStartupChanged,
		WindowSizeChanged,
		LogLevelChanged;

	public static List<Install> Installs {
		get => _installs;
		set {
			_installs = value;
			InstallsChanged?.Invoke();
		}
	}
	
	public static Install? SelectedInstall => _installs.FirstOrDefault(i => i.Id == _selectedInstallId);
	
	public static string? InstallPath => SelectedInstall?.InstallPath;

	public static void SelectInstall(string? installId) {
		if (_selectedInstallId == installId) return;
		
		_selectedInstallId = installId;
		InstallPathChanged?.Invoke();
		Logger.Log(LogLevel.Info, $"Selected install changed to: {SelectedInstall?.DisplayName ?? "None"}");
	}
	
	public static void AddInstall(Install install) {
		if (_installs.Any(i => i.Id == install.Id)) return;
		
		_installs.Add(install);
		InstallsChanged?.Invoke();
		Logger.Log(LogLevel.Info, $"Added install: {install.DisplayName}");
	}
	
	public static void RemoveInstall(string installId) {
		var install = _installs.FirstOrDefault(i => i.Id == installId);
		if (install == null) return;
		
		_installs.Remove(install);
		
		if (_selectedInstallId == installId)
			SelectInstall(_installs.FirstOrDefault()?.Id);
		
		InstallsChanged?.Invoke();
		Logger.Log(LogLevel.Info, $"Removed install: {install.DisplayName}");
	}
	
	public static void TriggerInstallsChanged() {
		InstallsChanged?.Invoke();
	}

	public static bool AllowStartupWithConflicts {
		get => _allowStartupWithConflicts;
		set { 	
			_allowStartupWithConflicts = value;
			StartupWithConflictsChanged?.Invoke();
			Logger.Log(LogLevel.Debug, $"Setting {nameof(AllowStartupWithConflicts)} changed to: {value}");
		}
	}

	public static bool IsPatched {
		get => SelectedInstall?.IsPatched ?? true;
		set {
			if (SelectedInstall == null) return;
			if (SelectedInstall.IsPatched == value) return;
			SelectedInstall.IsPatched = value;
			PatchStatusChanged?.Invoke();
			Logger.Log(LogLevel.Debug, $"Setting {nameof(IsPatched)} changed to: {value}");
		}
	}

	public static bool CheckForUpdatesOnStartup {
		get => _checkForUpdatesOnStartup;
		set { 	
			if (_checkForUpdatesOnStartup == value) return;
			_checkForUpdatesOnStartup = value;
			CheckForUpdatesOnStartupChanged?.Invoke();
			Logger.Log(LogLevel.Debug, $"Setting {nameof(CheckForUpdatesOnStartup)} changed to: {value}");
		}
	}

	public static IReadOnlyList<SavedModState> LastKnownModState {
		get => SelectedInstall?.ModState.AsReadOnly() ?? new List<SavedModState>().AsReadOnly();
		set {
			if (SelectedInstall == null) return;
			SelectedInstall.ModState = value.ToList();
			ModStateChanged?.Invoke();
			Logger.Log(LogLevel.Debug, $"Setting {nameof(LastKnownModState)} changed");
		}
	}

	public static void UpdateModState(IEnumerable<Mod> mods) {
		if (SelectedInstall == null) return;
    
		SelectedInstall.ModState = mods.Select(mod => new SavedModState(mod.FolderName, mod.IsEnabled, mod.LoadOrder) {
			OptionValues = new Dictionary<string, object?>(mod.OptionValues)
		}).ToList();
		ModStateChanged?.Invoke();
	}

	public static Size WindowSize {
		get => _windowSize;
		set { 	
			if (_windowSize == value) return;
			_windowSize = value;
			WindowSizeChanged?.Invoke();
			Logger.Log(LogLevel.Debug, $"Setting {nameof(WindowSize)} changed to: {value}");
		}
	}
	
	public static LogLevel LogLevel {
		get => _logLevel;
		set { 	
			if (_logLevel == value) return;
			_logLevel = value;
			LogLevelChanged?.Invoke();
			Logger.Log(LogLevel.Debug, $"Setting {nameof(LogLevel)} changed to: {value}");
		}
	}
}