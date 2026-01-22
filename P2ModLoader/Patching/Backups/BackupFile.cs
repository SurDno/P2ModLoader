namespace P2ModLoader.Patching.Backups;

public class BackupFile(string backupPath, string originalPath, string relativePath) {
	public string BackupPath { get; init; } = backupPath;
	public string OriginalPath { get; init; } = originalPath;
	public string RelativePath { get; init; } = relativePath;
}