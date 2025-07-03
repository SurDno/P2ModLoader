using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using P2ModLoader.Helper;
using P2ModLoader.Logging;

namespace P2ModLoader.Update;

public static partial class AutoUpdater {
    private const string OWNER = "SurDno";
    private const string REPO = "P2ModLoader";
    
    private static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string UpdateDirectory = Path.Combine(BaseDirectory, "Updates");

    
    public static async Task CheckForUpdatesAsync(bool showNoUpdatesDialog = false) {
        using var perf = PerformanceLogger.Log();
        Logger.Log(LogLevel.Info, $"Initiating update check...");
        try {
            var releases = await GitHubDownloader.GetAllReleasesAsync(OWNER, REPO);
            if (releases == null || releases.Count == 0 || releases[0].Assets.Count == 0) {
                ErrorHandler.Handle("No available versions found. Check your internet connection", null);
                return;
            }
            
            var latestRelease = releases[0];
            var newVersion = latestRelease.TagName.TrimStart('v');
            Logger.Log(LogLevel.Info, $"Latest version is: {newVersion}, current version is: {VersionComparison.CurrentLoaderVersion}");

            if (!VersionComparison.IsLoaderNewer(newVersion)) {
                if (showNoUpdatesDialog)
                    MessageBox.Show("No new versions found.", "No updates", MessageBoxButtons.OK);
                return;
            }

            var message = $"A new update is available ({newVersion}).\n" +
                          $"Changes from current version ({VersionComparison.CurrentLoaderVersion}):\n\n" +
                          
                          $"{PatchnoteBuilder.GetCumulativeReleaseNotes(releases)}\n\n" +
                          
                          $"Do you want to update?";
            var result = MessageBox.Show(message, "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
                await DownloadAndInstallUpdateAsync(latestRelease);
        } catch (Exception ex) {
            ErrorHandler.Handle("Failed to check for updates", ex);
        }
    }

    private static async Task DownloadAndInstallUpdateAsync(GitHubRelease release) {
        using var perf = PerformanceLogger.Log();
        Directory.CreateDirectory(UpdateDirectory);

        var assetUrl = release.Assets[0].BrowserDownloadUrl;
        var zipPath = Path.Combine(UpdateDirectory, "update.zip");

        await GitHubDownloader.DownloadReleaseAssetAsync(assetUrl, zipPath);

        var extractPath = Path.Combine(UpdateDirectory, "extracted");
        if (Directory.Exists(extractPath))
            Directory.Delete(extractPath, true);
        Directory.CreateDirectory(extractPath);

        ZipFile.ExtractToDirectory(zipPath, extractPath);

        var updateScript = CreateUpdateScript(extractPath);
        var startInfo = new ProcessStartInfo {
            FileName = updateScript,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(updateScript)!
        };
    
        Logger.Log(LogLevel.Info, $"Attempting to start update script...");
        Process? process;
        try {
            process = Process.Start(startInfo);
        } catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) {
            MessageBox.Show("Update process was cancelled by user.", "Update Cancelled", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

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
}