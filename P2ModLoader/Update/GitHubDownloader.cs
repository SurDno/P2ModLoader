using System.Text.Json;
using P2ModLoader.Logging;

namespace P2ModLoader.Update;

public static class GitHubDownloader {
	private static readonly HttpClient Client;
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
	
	static GitHubDownloader() { 	
		Client = new HttpClient();
		Client.DefaultRequestHeaders.Add("User-Agent", "Auto-Updater");
	}
	
	public static async Task<List<GitHubRelease>?> GetAllReleasesAsync(string owner, string repo) { 	
		try {
			var response = await Client.GetStringAsync($"https://api.github.com/repos/{owner}/{repo}/releases");
			return JsonSerializer.Deserialize<List<GitHubRelease>>(response, JsonOptions) ?? [];
		} catch {
			return null;
		}
	}
    
	public static async Task DownloadReleaseAssetAsync(string downloadUrl, string destinationPath) { 	
		Logger.Log(LogLevel.Info, $"Downloading asset from: {downloadUrl}");
        
		try {
			await using var stream = await Client.GetStreamAsync(downloadUrl);
			await using var fileStream = File.Create(destinationPath);
			await stream.CopyToAsync(fileStream);
            
			Logger.Log(LogLevel.Info, $"Successfully downloaded asset to: {destinationPath}");
		} catch (Exception ex) {
			Logger.Log(LogLevel.Error, $"Failed to download asset: {ex.Message}");
			throw;
		}
	}
}