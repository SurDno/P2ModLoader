using System.Runtime.CompilerServices;
using P2ModLoader.Helper;

namespace P2ModLoader.Logging;

public static class Logger {
    private static readonly string ExeLogFilePath;
    private static readonly object LockObject = new();
    private static readonly List<string> BufferedLogs = [];
    private static string? _lastInstallLogPath;

    static Logger() {
        var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        Directory.CreateDirectory(logDirectory);
        ExeLogFilePath = Path.Combine(logDirectory, "P2ModLoader.log");
        File.Delete(ExeLogFilePath);
    }

    public static string GetLogPath() => GetInstallLogPath() ?? ExeLogFilePath;

    private static string? GetInstallLogPath() {
        if (string.IsNullOrEmpty(SettingsHolder.InstallPath))
            return null;

        var installLogDirectory = Path.Combine(SettingsHolder.InstallPath, "Logs");
        return !Directory.Exists(installLogDirectory) ? null : Path.Combine(installLogDirectory, "P2ModLoader.log");
    }

    private static void HandleInstallPathChange(string newInstallLogPath) {
        if (newInstallLogPath != _lastInstallLogPath) {
            File.WriteAllLines(newInstallLogPath, BufferedLogs);
            _lastInstallLogPath = newInstallLogPath;
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

    public static void Log(LogLevel lvl, [InterpolatedStringHandlerArgument("lvl")] LogInterpolatedStringHandler handler) {
        if (lvl > SettingsHolder.LogLevel || SettingsHolder.LogLevel == LogLevel.None)
            return;

        WriteToLogs($"{lvl.ToString().ToUpper()}: {handler.ToString()}");
    }

    public static void LogLineBreak(LogLevel lvl) {
        if (lvl > SettingsHolder.LogLevel || SettingsHolder.LogLevel == LogLevel.None) return;
        WriteToLogs(string.Empty, timestamped: false);
    }
}