using System.Text.RegularExpressions;
using Microsoft.Win32;
using P2ModLoader.Data;
using P2ModLoader.Logging;

namespace P2ModLoader.Helper;

public static class InstallationLocator {
    private const string STEAM_32_BIT_PATH = @"SOFTWARE\Valve\Steam";
    private const string STEAM_64_BIT_PATH = @"SOFTWARE\Wow6432Node\Valve\Steam";
    private const string STEAM_LINUX_PATH = @"\.local\share\Steam";
    private const string STEAM_LIBRARY_FOLDERS_PATH = @"config\libraryfolders.vdf";
    private const string STEAM_APPS_RELATIVE_PATH = @"steamapps\common";
    private const string APPDATA_PATH = @"AppData\LocalLow\Ice-Pick Lodge\Pathologic 2";

    public static string SteamExe = "steam.exe";

    private static readonly Dictionary<string, Game> SteamGameFolders = new() {
        { "Pathologic Demo", Game.MarbleNest },
        { "Pathologic", Game.Pathologic2 },
        { "Pathologic 3 Quarantine", Game.Pathologic3Quarantine },
        { "Pathologic 3 Demo", Game.Pathologic3Demo },
        { "Pathologic 3", Game.Pathologic3 }
    };

    private static readonly Dictionary<uint, Game> GogGameIds = new() {
        { 1541120073, Game.Pathologic2Demo },
        { 1076642617, Game.Pathologic2 },
        { 1567359699, Game.Pathologic3 }
    };
    
    public static string? FindSteam() {
        var steamPath = GetSteamPathFromRegistry(STEAM_32_BIT_PATH);
        steamPath ??= GetSteamPathFromRegistry(STEAM_64_BIT_PATH);

        // TODO: better way of parsing if we're on Linux and where Steam is.
        if (steamPath == null && Path.Exists(@"Z:\home")) {
            steamPath = Path.Join(@"Z:\home\", Environment.GetEnvironmentVariable("USERNAME"), STEAM_LINUX_PATH);
            steamPath = Path.Exists(steamPath) ? steamPath : null;
            SteamExe = Path.Exists(steamPath) ? "steam.sh" : SteamExe;
        }

        return steamPath;
    }

    private static string? GetSteamPathFromRegistry(string registryPath) {
        using var key = Registry.LocalMachine.OpenSubKey(registryPath);
        return key?.GetValue("InstallPath") as string;
    }

    public static string? FindAppData() {
        var userPath = Environment.GetEnvironmentVariable("USERPROFILE");
        var appdataPath = Path.Combine(userPath!, APPDATA_PATH);
        Logger.Log(LogLevel.Debug, $"Checking AppData path: {appdataPath}");

        return Directory.Exists(appdataPath) ? appdataPath : null;
    }

    public static List<Install> LocateAllInstalls(List<Install> existingInstalls) {
        var newInstalls = new List<Install>();
        var existingPaths = existingInstalls.Select(i => NormalizePath(i.InstallPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var foundInstalls = FindSteamInstalls();
        
        foreach (var install in foundInstalls) {
            var normalizedPath = NormalizePath(install.InstallPath);
            if (!existingPaths.Contains(normalizedPath))
                newInstalls.Add(install);
        }

        if (newInstalls.Count > 0) {
            var installNames = string.Join(", ", newInstalls.Select(i => i.DisplayName));
            Logger.Log(LogLevel.Info, $"Added {newInstalls.Count} new install(s): {installNames}");
        } else {
            Logger.Log(LogLevel.Info, $"No new installs found");
        }

        return newInstalls;
    }

    private static string NormalizePath(string path) {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static List<Install> FindSteamInstalls() {
        var installs = new List<Install>();
        var steamPath = FindSteam();

        if (string.IsNullOrEmpty(steamPath)) {
            Logger.Log(LogLevel.Info, $"Steam not found, skipping Steam installs");
            return installs;
        }

        var libraryFoldersPath = Path.Combine(steamPath, STEAM_LIBRARY_FOLDERS_PATH);
        if (!File.Exists(libraryFoldersPath)) {
            libraryFoldersPath = Path.Combine(steamPath, STEAM_LIBRARY_FOLDERS_PATH);
            if (!File.Exists(libraryFoldersPath)) {
                Logger.Log(LogLevel.Warning, $"Steam found but library folders file is missing");
                return installs;
            }
        }

        var libraryPaths = GetSteamLibraryPaths(libraryFoldersPath);
        Logger.Log(LogLevel.Debug, $"Found {libraryPaths.Count} Steam library path(s)");

        foreach (var libraryPath in libraryPaths) {
            var commonPath = Path.Combine(libraryPath, STEAM_APPS_RELATIVE_PATH);
            
            if (!Directory.Exists(commonPath)) {
                Logger.Log(LogLevel.Debug, $"Common folder not found in library: {libraryPath}");
                continue;
            }

            foreach (var (folderName, game) in SteamGameFolders) {
                var gamePath = Path.Combine(commonPath, folderName);
                
                if (!Directory.Exists(gamePath)) continue;

                var expectedExe = game switch {
                    Game.Pathologic2 or Game.MarbleNest => "Pathologic.exe",
                    _ => "Pathologic3.exe"
                };

                if (!File.Exists(Path.Combine(gamePath, expectedExe))) {
                    Logger.Log(LogLevel.Debug, $"Found folder {folderName} but missing {expectedExe}");
                    continue;
                }

                var install = new Install(gamePath, game) {
                    CustomLabel = "Steam",
                    IsSteamInstall = true
                };

                installs.Add(install);
                Logger.Log(LogLevel.Debug, $"Detected {game} (Steam) at {gamePath}");
            }
        }

        return installs;
    }

    private static List<string> GetSteamLibraryPaths(string libraryFoldersPath) {
        List<string> paths = [];
        var content = File.ReadAllText(libraryFoldersPath);
        
        var pathRegex = new Regex(@"""path""\s+""([^""]+)""");
        var matches = pathRegex.Matches(content);
        
        foreach (Match match in matches) {
            if (match.Success) 
                paths.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
        }

        return paths;
    }
    
    public static bool IsSteamInstall(string installPath) {
        var steamPath = FindSteam();
        if (string.IsNullOrEmpty(steamPath)) return false;

        var libraryFoldersPath = Path.Combine(steamPath, STEAM_LIBRARY_FOLDERS_PATH);
        if (!File.Exists(libraryFoldersPath)) {
            libraryFoldersPath = Path.Combine(steamPath, STEAM_LIBRARY_FOLDERS_PATH.ToLower());
            if (!File.Exists(libraryFoldersPath)) return false;
        }

        var libraryPaths = GetSteamLibraryPaths(libraryFoldersPath);
        var normalziedInstallPath = NormalizePath(installPath);

        return libraryPaths.Select(libraryPath => Path.Combine(libraryPath, STEAM_APPS_RELATIVE_PATH)).
            Any(commonPath => SteamGameFolders.Keys.Select(folderName => Path.Combine(commonPath, folderName)).
                Select(NormalizePath).Any(normalizedSteamPath => normalziedInstallPath.
                    Equals(normalizedSteamPath, StringComparison.OrdinalIgnoreCase)));
    }
    
    public static Game? DetectGameType(string installPath) {
        // TODO: Add Alpha support.
        if (File.Exists(Path.Combine(installPath, "Pathologic.exe"))) {
            var appInfo = Path.Combine(installPath, "Pathologic_Data", "app.info");
            if (!File.Exists(appInfo)) return null;
            var content = File.ReadAllText(appInfo);
            if (content.Contains("Pathologic") && !content.Contains("Pathologic 2"))
                return Game.MarbleNest;
            
            var monoBleedingEdgeConfig = Path.Combine(installPath, "MonoBleedingEdge/etc/mono/config");
            if (!File.Exists(monoBleedingEdgeConfig)) return null;
            var content2 = File.ReadAllText(monoBleedingEdgeConfig);
            if (content2.Contains("buildslave"))
                return Game.Pathologic2Demo;
            
            return Game.Pathologic2;
        }
    
        if (File.Exists(Path.Combine(installPath, "Pathologic3.exe"))) {
            var appInfoPath = Path.Combine(installPath, "Pathologic3_Data", "app.info");
            if (!File.Exists(appInfoPath)) return null;
            var content = File.ReadAllText(appInfoPath);
            if (content.Contains("Quarantine", StringComparison.OrdinalIgnoreCase))
                return Game.Pathologic3Quarantine;
            if (content.Contains("Demo", StringComparison.OrdinalIgnoreCase))
                return Game.Pathologic3Demo;
            return Game.Pathologic3;
        }
    
        return null;
    }
}