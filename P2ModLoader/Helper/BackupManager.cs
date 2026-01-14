// P2ModLoader/Helper/BackupManager.cs
using P2ModLoader.Logging;
using System.Text.Json;

namespace P2ModLoader.Helper;

public static class BackupManager {
	private const string BACKUPS_RELATIVE_PATH = "Backups";
	private const string ADDED_FILES_TRACKER = "added_files.json";

	private static string BackupFolderPath => Path.Combine(SettingsHolder.InstallPath!, BACKUPS_RELATIVE_PATH);
	private static string AddedFilesPath => Path.Combine(BackupFolderPath, ADDED_FILES_TRACKER);

	public static bool TryRecoverBackups() {
		if (SettingsHolder.InstallPath == null) return false;

		if (!Directory.Exists(BackupFolderPath))
			return true;

		foreach (var backup in Directory.GetFiles(BackupFolderPath, "*.*", SearchOption.AllDirectories)) {
			if (Path.GetFileName(backup) == ADDED_FILES_TRACKER) continue;

			var relativePath = Path.GetRelativePath(BackupFolderPath, backup);
			var originalPath = Path.Combine(SettingsHolder.InstallPath, relativePath);

			if (!File.Exists(originalPath)) {
				ErrorHandler.Handle("A backup is present for a file not present in the original directory. " +
				                    "The backup could not be restored properly", null);
				return false;
			}

			File.Copy(backup, originalPath, true);
			File.Delete(backup);
		}

		if (File.Exists(AddedFilesPath)) {
			try {
				var addedFilesJson = File.ReadAllText(AddedFilesPath);
				var addedFiles = JsonSerializer.Deserialize<List<string>>(addedFilesJson) ?? [];

				foreach (var addedFile in addedFiles) {
					var fullPath = Path.Combine(SettingsHolder.InstallPath, addedFile);
					if (!File.Exists(fullPath)) continue;
					File.Delete(fullPath);
					Logger.Log(LogLevel.Info, $"Deleted added file: {addedFile}");
				}

				File.Delete(AddedFilesPath);
			} catch (Exception ex) {
				Logger.Log(LogLevel.Warning, $"Failed to process added files tracker: {ex.Message}");
			}
		}

		Directory.Delete(BackupFolderPath, true);
		return true;
	}

	public static string? CreateBackupOrTrack(string filePath) {
		if (SettingsHolder.InstallPath == null) return null;

		if (!Directory.Exists(BackupFolderPath))
			Directory.CreateDirectory(BackupFolderPath);
		
		var relativePath = Path.GetRelativePath(SettingsHolder.InstallPath, filePath);

		if (!File.Exists(filePath)) {
			TrackAddedFile(relativePath);
			Logger.Log(LogLevel.Debug, $"Tracking new file: {relativePath}");
			return null;
		}

		var backupPath = Path.Combine(BackupFolderPath, relativePath);

		try {
			Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
		} catch {
			Logger.Log(LogLevel.Error, $"Cannot create necessary directory: {relativePath} {backupPath}");
		}

		if (!File.Exists(backupPath))
			File.Copy(filePath, backupPath, false);

		return backupPath;
	}

	private static void TrackAddedFile(string relativePath) {
		try {
			List<string> addedFiles = [];

			if (File.Exists(AddedFilesPath)) {
				var existingJson = File.ReadAllText(AddedFilesPath);
				addedFiles = JsonSerializer.Deserialize<List<string>>(existingJson) ?? [];
			}

			if (!addedFiles.Contains(relativePath, StringComparer.OrdinalIgnoreCase)) {
				addedFiles.Add(relativePath);

				var json = JsonSerializer.Serialize(addedFiles, new JsonSerializerOptions { WriteIndented = true });
				File.WriteAllText(AddedFilesPath, json);
			}
		} catch (Exception ex) {
			Logger.Log(LogLevel.Warning, $"Failed to track added file {relativePath}: {ex.Message}");
		}
	}

	public static string? GetBackupPath(string filePath) {
		if (SettingsHolder.InstallPath == null)
			return null;
        
		var backupPath = Path.Combine(BackupFolderPath, Path.GetRelativePath(SettingsHolder.InstallPath, filePath));
		return File.Exists(backupPath) ? backupPath : null;
	}
}