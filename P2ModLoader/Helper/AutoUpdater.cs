using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using P2ModLoader.Logging;

namespace P2ModLoader.Helper;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class GitHubRelease {
    [JsonPropertyName("tag_name")] public string TagName { get; set; } = string.Empty;
    [JsonPropertyName("assets")] public List<GitHubAsset> Assets { get; set; } = [];
    [JsonPropertyName("body")] public string Body { get; set; } = string.Empty;
}

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class GitHubAsset {
    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;
}

public static partial class AutoUpdater {
    private const string OWNER = "SurDno";
    private const string REPO = "P2ModLoader";

    public static readonly string CurrentVersion;
    private static readonly HttpClient Client;
    private static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string UpdateDirectory = Path.Combine(BaseDirectory, "Updates");

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    static AutoUpdater() {
        using var perf = PerformanceLogger.Log();
        var versionInfo = Assembly.GetExecutingAssembly().GetName().Version!;
        CurrentVersion = $"{versionInfo.Major}.{versionInfo.Minor}.{versionInfo.Build}";

        Client = new HttpClient();
        Client.DefaultRequestHeaders.Add("User-Agent", "Auto-Updater");
    }
    
    public static async Task CheckForUpdatesAsync(bool showNoUpdatesDialog = false) {
        using var perf = PerformanceLogger.Log();
        Logger.Log(LogLevel.Info, $"Initiating update check...");
        try {
            var releases = await GetAllReleasesAsync();
            if (releases == null || releases.Count == 0 || releases[0].Assets.Count == 0) {
                ErrorHandler.Handle("No available versions found. Check your internet connection.", null);
                return;
            }

            var latestRelease = releases[0];
            var newVersion = latestRelease.TagName.TrimStart('v');
            Logger.Log(LogLevel.Info, $"Latest version is: {newVersion}, current version is: {CurrentVersion}");

            if (!IsNewer(newVersion)) {
                if (showNoUpdatesDialog)
                    MessageBox.Show("No new versions found.", "No updates", MessageBoxButtons.OK);
                return;
            }

            var message = $"A new update is available ({newVersion}).\n" +
                          $"Changes from current version ({CurrentVersion}):\n\n" +
                          
                          $"{GetCumulativeReleaseNotes(releases)}\n\n" +
                          
                          $"Do you want to update?";
            var result = MessageBox.Show(message, "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
                await DownloadAndInstallUpdateAsync(latestRelease);
        } catch (Exception ex) {
            ErrorHandler.Handle("Failed to check for updates", ex);
        }
    }

    private static async Task<List<GitHubRelease>?> GetAllReleasesAsync() {
        using var perf = PerformanceLogger.Log();
        try {
            var response = await Client.GetStringAsync($"https://api.github.com/repos/{OWNER}/{REPO}/releases");
            return JsonSerializer.Deserialize<List<GitHubRelease>>(response, JsonOptions) ?? [];
        } catch {
            return null;
        }
    }

    private static string GetCumulativeReleaseNotes(List<GitHubRelease> releases) {
        using var perf = PerformanceLogger.Log();
        var relevantReleases = releases.Where(r => IsNewer(r.TagName)).OrderBy(r => Version.Parse(r.TagName));

        var notes = new System.Text.StringBuilder();
        foreach (var release in relevantReleases) {
            var version = release.TagName.TrimStart('v');
            notes.AppendLine($"{version}:");
            var releaseBody = PatchnoteStartRegex().Replace(release.Body.Trim(), "- ");
            notes.AppendLine(releaseBody);
            notes.AppendLine();
        }

        return notes.ToString().TrimEnd();
    }

    public static bool IsNewer(string version) => Version.Parse(version) > Version.Parse(CurrentVersion);

    private static async Task DownloadAndInstallUpdateAsync(GitHubRelease release) {
        using var perf = PerformanceLogger.Log();
        Directory.CreateDirectory(UpdateDirectory);

        var assetUrl = release.Assets[0].BrowserDownloadUrl;
        var zipPath = Path.Combine(UpdateDirectory, "update.zip");

        await using (var stream = await Client.GetStreamAsync(assetUrl))
            await using (var fileStream = File.Create(zipPath))
                await stream.CopyToAsync(fileStream);

        var extractPath = Path.Combine(UpdateDirectory, "extracted");
        if (Directory.Exists(extractPath))
            Directory.Delete(extractPath, true);
        Directory.CreateDirectory(extractPath);

        ZipFile.ExtractToDirectory(zipPath, extractPath);

        var updateScript = CreateUpdateScript(extractPath);
        var startInfo = new ProcessStartInfo {
            FileName = "cmd.exe",
            Arguments = $"/c {updateScript}",
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = true,
            Verb = "runas"
        };
    
        Logger.Log(LogLevel.Info, $"Attempting to start update script...");
        var process = Process.Start(startInfo);
    
        await Task.Delay(500);
        if (process != null) {
            Logger.Log(LogLevel.Info, $"Update script started with PID: {process.Id}.");
            Environment.Exit(0);
        } else {
            Logger.Log(LogLevel.Error, $"Failed to start update script.");
            MessageBox.Show("An update was downloaded but failed to replace the current version automatically." +
                            $"To update, close P2ModLoader, go to {extractPath} and launch update.bat manually.",
                            "Auto-update error", MessageBoxButtons.OK);
        }
    }

    private static string CreateUpdateScript(string updatePath) {
        using var perf = PerformanceLogger.Log();
        var scriptPath = Path.Combine(UpdateDirectory, "update.bat");
        var currentExe = Environment.ProcessPath;

        var script = $"""
                      @echo off

                      timeout /t 1 /nobreak > nul

                      :wait_loop
                      tasklist | find /i "P2ModLoader.exe" > nul
                      if not errorlevel 1 (
                          timeout /t 1 > nul
                          goto wait_loop
                      )

                      rem Delete all files in the main directory except Updates folder
                      for /F "delims=" %%i in ('dir /b "{BaseDirectory}"') do (
                          if /I not "%%i"=="Updates" if /I not "%%i"=="Settings" if /I not "%%i"=="Logs" (
                              if exist "{BaseDirectory}%%i\*" (
                                  rd /s /q "{BaseDirectory}%%i"
                              ) else (
                                  del /q "{BaseDirectory}%%i"
                              )
                          )
                      )

                      rem Copy all files from update to main directory
                      xcopy "{updatePath}\*" "{BaseDirectory}" /E /H /C /I /Y

                      rem Clean up Updates directory
                      rd /s /q "{updatePath}"
                      del /q "{UpdateDirectory}\update.zip"

                      rem Start the updated application
                      start /B "" "{currentExe}"

                      rem Delete this script
                      del "%~f0"
                      """;

        File.WriteAllText(scriptPath, script);
        return scriptPath;
    }

    [GeneratedRegex(@"(?m)^\* ")]
    private static partial Regex PatchnoteStartRegex();
}