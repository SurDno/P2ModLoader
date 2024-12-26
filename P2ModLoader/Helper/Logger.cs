namespace P2ModLoader.Helper;

public static class Logger {
    private static readonly string ExeLogFilePath;
    private static readonly object LockObject = new();
    private static readonly List<string> BufferedLogs = new();
    private static string? _lastInstallLogPath;

    static Logger() {
        var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        Directory.CreateDirectory(logDirectory);
        ExeLogFilePath = Path.Combine(logDirectory, "P2ModLoader.log");
        File.Delete(ExeLogFilePath);
    }

    private static string? GetInstallLogPath() {
        if (string.IsNullOrEmpty(SettingsHolder.InstallPath))
            return null;

        var installLogDirectory = Path.Combine(SettingsHolder.InstallPath, "Logs");
        return !Directory.Exists(installLogDirectory) ? null : Path.Combine(installLogDirectory, "P2ModLoader.log");
    }
    
    private static void HandleInstallPathChange(string? newInstallLogPath) {
        if (newInstallLogPath != null && newInstallLogPath != _lastInstallLogPath) {
            File.WriteAllLines(newInstallLogPath, BufferedLogs);
            _lastInstallLogPath = newInstallLogPath;
        } else if (newInstallLogPath == null) {
            _lastInstallLogPath = null;
        }
    }

    private static void WriteToLogs(string content, bool timestamped = true) {
        var logMessage = timestamped ? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {content}" : content;
        Console.WriteLine(logMessage);

        try {
            lock (LockObject) {
                File.AppendAllText(ExeLogFilePath, logMessage + Environment.NewLine);
                BufferedLogs.Add(logMessage);

                var installLogPath = GetInstallLogPath();
                if (installLogPath == null) return;
                HandleInstallPathChange(installLogPath);
                File.AppendAllText(installLogPath, logMessage + Environment.NewLine);
            }
        } catch (Exception ex) {
            ErrorHandler.Handle($"Error writing to log file: {ex.Message}", ex, skipLogging: true);
        }
    }

    public static void LogError(string message) => WriteToLogs($"ERROR: {message}");
    public static void LogWarning(string message) => WriteToLogs($"WARNING: {message}");
    public static void LogInfo(string message) => WriteToLogs($"INFO: {message}");
    public static void LogLineBreak() => WriteToLogs(string.Empty, timestamped: false);
}