namespace P2ModLoader.Patching.Backups;

public class BackupMetadata {
	public Dictionary<string, string> FileHashes { get; init; } = new();
	public DateTime BackupDate { get; init; }
}