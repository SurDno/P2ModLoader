using System.Text.RegularExpressions;
using Microsoft.Win32;
using P2ModLoader.Logging;

namespace P2ModLoader.Helper;

public static class InstallationLocator {
    private const string STEAM_32_BIT_PATH = @"SOFTWARE\Valve\Steam";
    private const string STEAM_64_BIT_PATH = @"SOFTWARE\Wow6432Node\Valve\Steam";

    private const string STEAM_LINUX_PATH = @"\.local\share\Steam";

    public static string SteamExe = "steam.exe";
    
    private const string PATHOLOGIC_2_STEAM_APP_ID = "505230";

    private const string STEAM_LIBRARY_FOLDERS_PATH = @"config\libraryfolders.vdf";
    private const string PATHOLOGIC_STEAM_RELATIVE_PATH = @"steamapps\common\Pathologic";

    private const string APPDATA_PATH = @"AppData\LocalLow\Ice-Pick Lodge\Pathologic 2";
    private const string LOGFILE_NAME = @"output_log.txt";
    
    public static string? FindSteam() { 	
        var steamPath = GetSteamPathFromRegistry(STEAM_32_BIT_PATH);
        steamPath ??= GetSteamPathFromRegistry(STEAM_64_BIT_PATH);

        if (steamPath == null && Path.Exists(@"Z:\home")) {
            // Looking for Linux + Wine setup
            steamPath = Path.Join(@"Z:\home\", Environment.GetEnvironmentVariable("USERNAME"), STEAM_LINUX_PATH);
            steamPath = Path.Exists(steamPath) ? steamPath : null;
            SteamExe = Path.Exists(steamPath) ? "steam.sh" : SteamExe;
        }
        
        return steamPath;
    }

    public static string? FindInstall() { 	
        var steamPath = FindSteam();
        if (!string.IsNullOrEmpty(steamPath)) {
            Logger.Log(LogLevel.Info, $"Found Steam installation: {steamPath}");
            return FindSteamInstall(steamPath);
        }

        // TODO: Handle GOG installs.
        return null;
    }

    private static string? FindSteamInstall(string steamPath) { 	
        var libraryFoldersPath = Path.Combine(steamPath, STEAM_LIBRARY_FOLDERS_PATH);
        if (!File.Exists(libraryFoldersPath)) {
            libraryFoldersPath = Path.Combine(steamPath, STEAM_LIBRARY_FOLDERS_PATH.ToLower());
            if (!File.Exists(libraryFoldersPath)) {
                return null;
            }
        }

        var installPath = FindPathologicSteamPath(libraryFoldersPath);
        Logger.Log(LogLevel.Info, $"installPath: {installPath}");
        return installPath;
    }

    private static string? GetSteamPathFromRegistry(string registryPath) { 	
        using var key = Registry.LocalMachine.OpenSubKey(registryPath);
        return key?.GetValue("InstallPath") as string;
    }

    private static string? FindPathologicSteamPath(string libraryFoldersPath) { 	
        var content = File.ReadAllText(libraryFoldersPath);

        var pathRegex = new Regex("\"path\"\\s+\"([^\"]+)\"");
        var appRegex = new Regex($"\"{PATHOLOGIC_2_STEAM_APP_ID}\"\\s+\"[^\"]+\"");

        var lines = content.Split('\n');
        string? currentPath = null;
        var foundApp = false;

        foreach (var t in lines) {
            var line = t.Trim();

            var pathMatch = pathRegex.Match(line);
            if (pathMatch.Success) {
                currentPath = pathMatch.Groups[1].Value.Replace(@"\\", @"\");
            }

            if (currentPath != null && appRegex.IsMatch(line)) {
                foundApp = true;
                break;
            }

            if (line == "}")
                currentPath = null;
        }

        if (!foundApp)
            return null;

        return currentPath != null ? Path.Combine(currentPath, PATHOLOGIC_STEAM_RELATIVE_PATH) : null;
    }

    public static string? FindAppData() { 	
        var userPath = Environment.GetEnvironmentVariable("USERPROFILE");
        var appdataPath = Path.Combine(userPath!, APPDATA_PATH);
        Logger.Log(LogLevel.Info, $"appdata\n{userPath}\n{appdataPath}");

        return Directory.Exists(appdataPath) ? appdataPath : null;
    }

    public static string FindLogFile() { 	
        return Path.Combine(FindAppData()!, LOGFILE_NAME);
    }
}