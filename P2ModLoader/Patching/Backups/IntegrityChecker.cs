using System.Security.Cryptography;
using P2ModLoader.Helper;
using P2ModLoader.Logging;

namespace P2ModLoader.Patching.Backups;

public static class IntegrityChecker {
	public static void PerformStartupIntegrityCheck() {
		if (SettingsHolder.InstallPath == null) return;

		var backupFolderPath = Path.Combine(SettingsHolder.InstallPath, "Backups");
		if (!Directory.Exists(backupFolderPath)) return;

		var metadata = BackupManager.LoadMetadata();
		if (metadata == null) return;
       
		var modifiedCount = CheckIntegrity(SettingsHolder.InstallPath, metadata);

		if (modifiedCount == 0) {
			Logger.Log(LogLevel.Info, $"Startup integrity check passed: all {metadata.FileHashes.Count} files match");
		} else {
			Logger.Log(LogLevel.Warning, $"Startup integrity check failed. This may indicate a game update.");
		}
	}

	private static int CheckIntegrity(string installPath, BackupMetadata metadata) {
		var modifiedCount = 0;

		foreach (var (relativePath, expectedHash) in metadata.FileHashes) {
			var originalPath = Path.Combine(installPath, relativePath);
			if (File.Exists(originalPath) && ComputeFileHash(originalPath) == expectedHash) continue;
			modifiedCount++;
			Logger.Log(LogLevel.Debug, $"File modified since backup: {relativePath}");
		}

		return modifiedCount;
	}

	public static string ComputeFileHash(string filePath) {
		using var sha256 = SHA256.Create();
		using var stream = File.OpenRead(filePath);
		return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
	}
}