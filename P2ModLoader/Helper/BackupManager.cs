namespace P2ModLoader.Helper;

public static class BackupManager {
	private const string BACKUPS_RELATIVE_PATH = "Backups";
	
	// Always call only after validating InstallPath is not null.
	private static string BackupFolderPath => Path.Combine(SettingsHolder.InstallPath!, BACKUPS_RELATIVE_PATH);
	
	public static bool TryRecoverBackups() {
		if (SettingsHolder.InstallPath == null)
			return false;

		if (!Directory.Exists(BackupFolderPath))
			return true;

		foreach (var backup in Directory.GetFiles(BackupFolderPath, "*.*", SearchOption.AllDirectories)) {
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
		
		Directory.Delete(BackupFolderPath, true);
		return true;
	}
	
	public static string? CreateBackup(string filePath) {
		if (SettingsHolder.InstallPath == null)
			return null;
		
		if (!Directory.Exists(BackupFolderPath))
			Directory.CreateDirectory(BackupFolderPath);
		
		var relativePath = Path.GetRelativePath(SettingsHolder.InstallPath, filePath);
		var backupPath = Path.Combine(BackupFolderPath, relativePath);

		Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
		
		// Never overwrite existing backups if two mods modify the same file.
		if(!File.Exists(backupPath))
			File.Copy(filePath, backupPath, false);

		return backupPath;
	}
	
	public static string? GetBackupPath(string filePath) {
		if (SettingsHolder.InstallPath == null)
			return null;
        
		var backupPath = Path.Combine(BackupFolderPath, Path.GetRelativePath(SettingsHolder.InstallPath, filePath));
		return File.Exists(backupPath) ? backupPath : null;
	}
}