using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using P2ModLoader.Logging;
using P2ModLoader.Patching.Xml;

namespace P2ModLoader.Data;

public class Install {
    [AutoPerformanceLogHook(AttributeExclude = true)]
    public string Id { get; }
    [AutoPerformanceLogHook(AttributeExclude = true)]
    public string InstallPath { get; set; }
    public Game Game { get; set; }
    public bool IsSteamInstall { get; set; }
    public string CustomLabel { get; set; } = string.Empty;
    public bool IsPatched { get; set; } = false;
    public List<SavedModState> ModState { get; set; } = [];
    
    public string GameName => Game switch {
        Game.MarbleNest => "The Marble Nest Demo",
        Game.Pathologic2Alpha => "Pathologic 2 Alpha",
        Game.Pathologic2Demo => "Pathologic 2 Demo",
        Game.Pathologic2 => "Pathologic 2",
        Game.Pathologic3Quarantine => "Pathologic 3: Quarantine",
        Game.Pathologic3Demo => "Pathologic 3: Demo",
        Game.Pathologic3 => "Pathologic 3",
        _ => throw new ArgumentOutOfRangeException(nameof(Game), Game, null)
    };
    
    public string DisplayName => !string.IsNullOrEmpty(CustomLabel) ? $"{GameName} ({CustomLabel})" : GameName;

    public string DisplayImage => Game switch {
        Game.MarbleNest => "tmn",
        Game.Pathologic3Quarantine => "p3q",
        Game.Pathologic3Demo => "p3d",
        Game.Pathologic3 => "p3",
        Game.Pathologic2Demo => "p2d",
        _ => "p2"
    };

    public string ModsPath => Path.Combine(InstallPath, "Mods");
    public string LogsPath => Path.Combine(InstallPath, "Logs");
    public string BackupsPath => Path.Combine(InstallPath, "Backups");
    
    public string AssetsPath => Game == Game.Pathologic2 ? "Pathologic_Data" : "Pathologic3_Data";
    public string ManagedPath => Path.Combine(AssetsPath, "Managed");
    public string FullManagedPath => Path.Combine(InstallPath, ManagedPath);
    public string FullAssetsPath => Path.Combine(InstallPath, AssetsPath);
    
    public string ExecutablePath => Game == Game.Pathologic2 
        ? Path.Combine(InstallPath, "Pathologic.exe")
        : Path.Combine(InstallPath, "Pathologic3.exe");

    public string? GameAppDataName => Game switch {
        Game.Pathologic2 => "Pathologic 2",
        Game.Pathologic3 => "Pathologic 3",
        Game.Pathologic3Demo => "Pathologic 3 Demo",
        Game.Pathologic3Quarantine => "Pathologic 3 Quarantine",
        _ => null
    };

    public string? PlayerLogName => Game switch {
        Game.Pathologic2 => "output_log.txt",
        Game.Pathologic3 or Game.Pathologic3Demo or Game.Pathologic3Quarantine => "Player.log",
        _ => null
    };

    [SuppressMessage("ReSharper", "SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault")]
    public uint SteamAppId {
        get {
            if (!IsSteamInstall) throw new InvalidOperationException($"Not a Steam install!");
            return Game switch {
                Game.MarbleNest => 607730,
                Game.Pathologic2 => 505230,
                Game.Pathologic3Quarantine => 3389330,
                Game.Pathologic3Demo => 4066100,
                Game.Pathologic3 => 3199650,
                _ => throw new InvalidOperationException("Trying to access Steam App ID on a non-Steam title.")
            };
        }
    }
    
    // For adding new installs
    public Install(string installPath, Game game) {
        Id = Guid.NewGuid().ToString();
        InstallPath = installPath;
        Game = game;
        IsPatched = true;
        ModState = [];
    }
    
    // For loading from settings.json
    public Install(string id, string installPath, Game game, string customLabel, bool isSteamInstall, bool isPatched, 
        List<SavedModState> modState) {
        Id = id;
        InstallPath = installPath;
        Game = game;
        CustomLabel = customLabel;
        IsSteamInstall = isSteamInstall;
        IsPatched = isPatched;
        ModState = modState ?? [];
    }
}
    
public class SavedInstall {
    public string Id { get; init; } = string.Empty;
    [JsonPropertyName("PathToInstall")] public string InstallPath { get; init; } = string.Empty;
    public string Game { get; init; } = string.Empty;
    public string CustomLabel { get; init; } = string.Empty;
    public bool IsSteamInstall { get; init; }
    public bool IsPatched { get; init; }
    public List<SavedModState> ModState { get; init; } = [];
}