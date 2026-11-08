namespace P2ModLoader.Patching.Backups;

public class BackupMetadata {
	public Dictionary<string, string> FileHashes { get; set; } = new();
	public DateTime BackupDate { get; set; }
}