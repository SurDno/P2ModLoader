using P2ModLoader.Data;
using P2ModLoader.Logging;

namespace P2ModLoader.Helper;

public static class SettingsHolder {
	private static string? _installPath;
	private static bool _allowStartupWithConflicts;
	private static bool _isPatched = true;
	private static bool _checkForUpdatesOnStartup = true;
	private static List<SavedModState> _lastKnownModState = [];
	private static Size _windowSize = new(600, 800);
	private static LogLevel _logLevel = LogLevel.Info;
	
	public static event Action? InstallPathChanged,
		StartupWithConflictsChanged,
		PatchStatusChanged,
		ModStateChanged,
		CheckForUpdatesOnStartupChanged,
		WindowSizeChanged,
		LogLevelChanged;

	public static string? InstallPath {
		get => _installPath;
		set {
			using var perf = PerformanceLogger.Log();
			var isValid = value != null && File.Exists(Path.Combine(value, "Pathologic.exe"));
        
			if (_installPath == value) return;
        
			_installPath = isValid ? value : null;
			InstallPathChanged?.Invoke();
			Logger.Log(LogLevel.Info, $"Setting {nameof(InstallPath)} changed to: {value}");
		}
	}

	public static bool AllowStartupWithConflicts {
		get => _allowStartupWithConflicts;
		set {
			using var perf = PerformanceLogger.Log();
			_allowStartupWithConflicts = value;
			StartupWithConflictsChanged?.Invoke();
			Logger.Log(LogLevel.Info, $"Setting {nameof(AllowStartupWithConflicts)} changed to: {value}");
		}
	}

	public static bool IsPatched {
		get => _isPatched;
		set {
			using var perf = PerformanceLogger.Log();
			if (_isPatched == value) return;
			_isPatched = value;
			PatchStatusChanged?.Invoke();
			Logger.Log(LogLevel.Info, $"Setting {nameof(IsPatched)} changed to: {value}");
		}
	}

	public static bool CheckForUpdatesOnStartup {
		get => _checkForUpdatesOnStartup;
		set {
			using var perf = PerformanceLogger.Log();
			if (_checkForUpdatesOnStartup == value) return;
			_checkForUpdatesOnStartup = value;
			CheckForUpdatesOnStartupChanged?.Invoke();
			Logger.Log(LogLevel.Info, $"Setting {nameof(CheckForUpdatesOnStartup)} changed to: {value}");
		}
	}

	public static IReadOnlyList<SavedModState> LastKnownModState {
		get => _lastKnownModState.AsReadOnly();
		set {
			using var perf = PerformanceLogger.Log();
			_lastKnownModState = value.ToList();
			ModStateChanged?.Invoke();
			Logger.Log(LogLevel.Info, $"Setting {nameof(LastKnownModState)} changed to: {value}");
		}
	}

	public static void UpdateModState(IEnumerable<Mod> mods) {
		using var perf = PerformanceLogger.Log();
		_lastKnownModState = mods.Select(mod => new SavedModState(
			mod.FolderName,
			mod.IsEnabled,
			mod.LoadOrder
		)).ToList();
		ModStateChanged?.Invoke();
	}

	public static Size WindowSize {
		get => _windowSize;
		set {
			using var perf = PerformanceLogger.Log();
			if (_windowSize == value) return;
			_windowSize = value;
			WindowSizeChanged?.Invoke();
			Logger.Log(LogLevel.Info, $"Setting {nameof(WindowSize)} changed to: {value}");
		}
	}
	
	public static LogLevel LogLevel {
		get => _logLevel;
		set {
			using var perf = PerformanceLogger.Log();
			if (_logLevel == value) return;
			_logLevel = value;
			LogLevelChanged?.Invoke();
			Logger.Log(LogLevel.Info, $"Setting {nameof(LogLevel)} changed to: {value}");
		}
	}
}