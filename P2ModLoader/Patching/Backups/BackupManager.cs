using System.Text.Json;
using P2ModLoader.Helper;
using P2ModLoader.Logging;

namespace P2ModLoader.Patching.Backups;

public static class BackupManager {
	private const string BACKUPS_RELATIVE_PATH = "Backups";
	private const string ADDED_FILES_TRACKER = "added_files.json";
	private const string BACKUP_METADATA = "backup_metadata.json";
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	private static string BackupFolderPath => Path.Combine(SettingsHolder.InstallPath!, BACKUPS_RELATIVE_PATH);
	private static string AddedFilesPath => Path.Combine(BackupFolderPath, ADDED_FILES_TRACKER);
	private static string MetadataPath => Path.Combine(BackupFolderPath, BACKUP_METADATA);

	public static bool TryRecoverBackups() {
		if (SettingsHolder.InstallPath == null) return false;
		if (!Directory.Exists(BackupFolderPath)) return true;

		var metadata = LoadMetadata();
		if (metadata == null) {
			Logger.Log(LogLevel.Warning, $"No backup metadata found, treating all files as modified.");
		}

		var (matching, modified) = CategorizeBackups(metadata);

		bool? restoreModified = null;
		if (modified?.Count > 0 || metadata == null) {
			restoreModified = ModifiedFilesPrompt.Show(matching.Count, modified, metadata?.BackupDate);
		
			if (restoreModified == null) {
				Logger.Log(LogLevel.Info, $"User cancelled backup restoration.");
				return false;
			}
		}

		RestoreFiles(matching);
		if (restoreModified == true) {
			RestoreFiles(modified ?? []);
			Logger.Log(LogLevel.Info, $"User chose to restore all files including potentially modified ones.");
		} else if (restoreModified == false) {
			DeleteBackups(modified ?? matching);
			Logger.Log(LogLevel.Info, $"User chose to keep modified files.");
		}

		RemoveAddedFiles();
		DeleteMetadata();
		CleanupBackupFolder();

		return true;
	}

	public static string? CreateBackupOrTrack(string filePath) {
		if (SettingsHolder.InstallPath == null) return null;
		if (!Directory.Exists(BackupFolderPath)) Directory.CreateDirectory(BackupFolderPath);
		
		var relativePath = Path.GetRelativePath(SettingsHolder.InstallPath, filePath);

		if (!File.Exists(filePath)) {
			TrackAddedFile(relativePath);
			Logger.Log(LogLevel.Debug, $"Tracking new file: {relativePath}");
			return null;
		}

		var backupPath = Path.Combine(BackupFolderPath, relativePath);
		Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);

		if (!File.Exists(backupPath)) {
			File.Copy(filePath, backupPath, false);
			Logger.Log(LogLevel.Debug, $"Created backup: {relativePath}");
		}

		return backupPath;
	}

	public static void SavePatchedFileHash(string filePath) {
		if (SettingsHolder.InstallPath == null) return;
		
		var relativePath = Path.GetRelativePath(SettingsHolder.InstallPath, filePath);
		var hash = IntegrityChecker.ComputeFileHash(filePath);
		
		var metadata = LoadMetadata() ?? new BackupMetadata { BackupDate = DateTime.UtcNow };
		metadata.FileHashes[relativePath] = hash;
		SaveMetadata(metadata);
		
		Logger.Log(LogLevel.Debug, $"Saved hash for patched file: {relativePath}");
	}

	public static string? GetBackupPath(string filePath) {
		if (SettingsHolder.InstallPath == null) return null;
		var backupPath = Path.Combine(BackupFolderPath, Path.GetRelativePath(SettingsHolder.InstallPath, filePath));
		return File.Exists(backupPath) ? backupPath : null;
	}

	public static BackupMetadata? LoadMetadata() {
		if (!File.Exists(MetadataPath)) return null;
		
		try {
			return JsonSerializer.Deserialize<BackupMetadata>(File.ReadAllText(MetadataPath));
		} catch (Exception ex) {
			Logger.Log(LogLevel.Error, $"Failed to load backup metadata: {ex.Message}");
			return null;
		}
	}

	private static void SaveMetadata(BackupMetadata metadata) {
		File.WriteAllText(MetadataPath, JsonSerializer.Serialize(metadata, JsonOptions));
	}

	private static void DeleteMetadata() {
		if (File.Exists(MetadataPath)) File.Delete(MetadataPath);
	}

	private static (List<BackupFile> matching, List<BackupFile>? modified) CategorizeBackups(BackupMetadata? metadata) {
		var matching = new List<BackupFile>();
		var modified = new List<BackupFile>();
		if (metadata == null) {
			foreach (var backup in Directory.GetFiles(BackupFolderPath, "*.*", SearchOption.AllDirectories)) {
				var fileName = Path.GetFileName(backup);
				if (fileName is ADDED_FILES_TRACKER or BACKUP_METADATA) continue;

				var relativePath = Path.GetRelativePath(BackupFolderPath, backup);
				var originalPath = Path.Combine(SettingsHolder.InstallPath!, relativePath);
				matching.Add(new BackupFile(backup, originalPath, relativePath));
			}

			return (matching, null);
		}

		foreach (var backup in Directory.GetFiles(BackupFolderPath, "*.*", SearchOption.AllDirectories)) {
			var fileName = Path.GetFileName(backup);
			if (fileName is ADDED_FILES_TRACKER or BACKUP_METADATA) continue;

			var relativePath = Path.GetRelativePath(BackupFolderPath, backup);
			var originalPath = Path.Combine(SettingsHolder.InstallPath!, relativePath);
			var file = new BackupFile(backup, originalPath, relativePath);

			if (metadata.FileHashes.TryGetValue(relativePath, out var expectedHash)) {
				if (IntegrityChecker.ComputeFileHash(originalPath) != expectedHash) {
					modified.Add(file);
				} else {
					matching.Add(file);
				}
			} else {
				matching.Add(file);
			}
		}


		return (matching, modified);
	}

	private static void RestoreFiles(List<BackupFile> files) {
		foreach (var file in files) {
			File.Copy(file.BackupPath, file.OriginalPath, true);
			Logger.Log(LogLevel.Info, $"Restored: {file.RelativePath}");
			File.Delete(file.BackupPath);
		}
	}

	private static void DeleteBackups(List<BackupFile> files) {
		foreach (var file in files) {
			File.Delete(file.BackupPath);
			Logger.Log(LogLevel.Info, $"Kept current version: {file.RelativePath}");
		}
	}

	private static void RemoveAddedFiles() {
		if (!File.Exists(AddedFilesPath)) return;

		try {
			var addedFiles = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(AddedFilesPath)) ?? [];
			foreach (var addedFile in addedFiles) {
				var fullPath = Path.Combine(SettingsHolder.InstallPath!, addedFile);
				if (!File.Exists(fullPath)) continue;
				File.Delete(fullPath);
				Logger.Log(LogLevel.Info, $"Deleted added file: {addedFile}");
			}
			File.Delete(AddedFilesPath);
		} catch (Exception ex) {
			Logger.Log(LogLevel.Warning, $"Failed to process added files tracker: {ex.Message}");
		}
	}

	private static void TrackAddedFile(string relativePath) {
		try {
			var addedFiles = File.Exists(AddedFilesPath)
				? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(AddedFilesPath)) ?? [] : [];

			if (addedFiles.Contains(relativePath, StringComparer.OrdinalIgnoreCase)) return;
			addedFiles.Add(relativePath);
			File.WriteAllText(AddedFilesPath, JsonSerializer.Serialize(addedFiles, JsonOptions));
		} catch (Exception ex) {
			Logger.Log(LogLevel.Warning, $"Failed to track added file {relativePath}: {ex.Message}");
		}
	}

	private static void CleanupBackupFolder() {
		if (!Directory.Exists(BackupFolderPath)) return;

		try {
			if (!Directory.EnumerateFileSystemEntries(BackupFolderPath).Any()) {
				Directory.Delete(BackupFolderPath, false);
				Logger.Log(LogLevel.Info, $"Removed empty backup folder");
			}
		} catch (Exception ex) {
			Logger.Log(LogLevel.Warning, $"Could not remove backup folder: {ex.Message}");
		}
	}
}