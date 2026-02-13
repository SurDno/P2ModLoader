using P2ModLoader.Logging;

namespace P2ModLoader.Update;

public static class InstallationValidator {
    private static readonly string[] AllowedExtensions = [".dll", ".json", ".exe", ".pdb", ".pspdb", ".pssym"];
    private static readonly string[] AllowedFolders = ["Resources", "Logs", "Updates", "Settings", 
        "cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant"];

    public static bool IsInValidInstallationFolder(out string errorMessage) {
        errorMessage = string.Empty;

        var baseDirectory = GetSafeBaseDirectory();

        var issues = new List<string>();

        issues.AddRange(Directory.EnumerateFiles(baseDirectory).Where(f => !AllowedExtensions.Contains(
                    Path.GetExtension(f).ToLowerInvariant())).Select(Path.GetFileName)!);
        issues.AddRange(Directory.EnumerateDirectories(baseDirectory).Select(Path.GetFileName)
                .Where(name => !AllowedFolders.Contains(name, StringComparer.OrdinalIgnoreCase))!);

        if (issues.Count > 0) {
            errorMessage = $"Found unexpected files or folders in P2ModLoader directory:\n" +
                           $"• {string.Join("\n• ", issues.Take(5))}" +
                           (issues.Count > 5 ? $"\n• ... and {issues.Count - 5} more" : "") +
                           $"\n\nThis most likely means P2ModLoader is not installed to its own folder.\n\n" +
                           $"P2ModLoader deletes the entire contents of the directory it's currently in when " +
                           $"auto-updating for a newer version. As this may lead to user file deletion, the update " +
                           $"functionality has been disabled. Please move P2ModLoader to its own folder to enable it.";
            return false;
        }

        Logger.Log(LogLevel.Debug, $"Installation validation passed: P2ModLoader is in its own folder");
        return true;
    }

    public static string GetSafeBaseDirectory() {
        var executablePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath)) {
            var directory = Path.GetDirectoryName(executablePath);
            if (!string.IsNullOrEmpty(directory)) {
                Logger.Log(LogLevel.Debug, $"Using executable directory: {directory}");
                return directory;
            }
        }
        
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        Logger.Log(LogLevel.Debug, $"Falling back to AppDomain BaseDirectory: {baseDir}");
        return baseDir;
    }
}