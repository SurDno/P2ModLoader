using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace P2ModLoader.Helper;

public static class InstallationLocator {
    private const string STEAM_32_BIT_PATH = @"SOFTWARE\Valve\Steam";
    private const string STEAM_64_BIT_PATH = @"SOFTWARE\Wow6432Node\Valve\Steam";

    private const string STEAM_LINUX_PATH = @"\.local\share\Steam";

    public static string STEAM_EXE = "steam.exe";
    
    private const string PATHOLOGIC_2_STEAM_APP_ID = "505230";

    private const string STEAM_LIBRARY_FOLDERS_PATH = @"config\libraryfolders.vdf";
    private const string PATHOLOGIC_STEAM_RELATIVE_PATH = @"steamapps\common\Pathologic";

    private const string APPDATA_PATH = @"AppData\LocalLow\Ice-Pick Lodge\Pathologic 2";

    public static string? FindSteam() {
        var steamPath = GetSteamPathFromRegistry(STEAM_32_BIT_PATH);
        steamPath ??= GetSteamPathFromRegistry(STEAM_64_BIT_PATH);

        if (steamPath == null && Path.Exists(@"Z:\home")) {
            // Looking for Linux + Wine setup
            steamPath = Path.Join(@"Z:\home\", Environment.GetEnvironmentVariable("USERNAME"), STEAM_LINUX_PATH);
            steamPath = Path.Exists(steamPath) ? steamPath : null;
            STEAM_EXE = Path.Exists(steamPath) ? "steam.sh" : STEAM_EXE;
        }
        
        return steamPath;
    }

    public static string? FindInstall() {
        var steamPath = FindSteam();
        if (!string.IsNullOrEmpty(steamPath)) {
            Logger.LogInfo("Found Steam installation:   " + steamPath);
            return FindSteamInstall(steamPath);
        };

        // TODO: Handle GOG installs.
        return null;
    }

    private static string? FindSteamInstall(string steamPath) {
        var libraryFoldersPath = Path.Combine(steamPath, STEAM_LIBRARY_FOLDERS_PATH);
        Logger.LogInfo("libraryFoldersPath: " + libraryFoldersPath + " ; " + File.Exists(libraryFoldersPath));
        if (!File.Exists(libraryFoldersPath)) {
            libraryFoldersPath = Path.Combine(steamPath, STEAM_LIBRARY_FOLDERS_PATH.ToLower());
            if (!File.Exists(libraryFoldersPath)) {
                return null;
            }
        }

        var installPath = FindPathologicSteamPath(libraryFoldersPath);
        Logger.LogInfo("installPath: " + installPath);
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
        Logger.LogInfo("appdata\n" + userPath + "\n" + appdataPath);

        return Directory.Exists(appdataPath) ? appdataPath : null;
    }
}