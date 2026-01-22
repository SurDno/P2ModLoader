using System.Text.Json;
using P2ModLoader.Data;
using P2ModLoader.Logging;

namespace P2ModLoader.Helper;

public static class SteamUpdateChecker {
	private static readonly HttpClient HttpClient = new();

	public static async Task<DateTime?> GetLastUpdateDate(uint steamAppId) {
		try {
			var url = $"https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid={steamAppId}&count=1&maxlength=0";
			var response = await HttpClient.GetStringAsync(url);
			
			using var doc = JsonDocument.Parse(response);
			var root = doc.RootElement;
			
			if (!root.TryGetProperty("appnews", out var appNews)) {
				Logger.Log(LogLevel.Warning, $"No appnews found for Steam App ID {steamAppId}");
				return null;
			}

			if (!appNews.TryGetProperty("newsitems", out var newsItems)) {
				Logger.Log(LogLevel.Warning, $"No newsitems found for Steam App ID {steamAppId}");
				return null;
			}

			var items = newsItems.EnumerateArray().ToList();
			if (items.Count == 0) {
				Logger.Log(LogLevel.Warning, $"No news items found for Steam App ID {steamAppId}");
				return null;
			}

			var latestItem = items[0];
			if (!latestItem.TryGetProperty("date", out var dateProperty)) {
				Logger.Log(LogLevel.Warning, $"No date found in news item for Steam App ID {steamAppId}");
				return null;
			}

			var unixTimestamp = dateProperty.GetInt64();
			var lastUpdate = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).DateTime;
			
			Logger.Log(LogLevel.Debug, $"Last update for Steam App ID {steamAppId}: {lastUpdate}");
			return lastUpdate;
		} catch (Exception ex) {
			Logger.Log(LogLevel.Error, $"Failed to check Steam updates for App ID {steamAppId}: {ex.Message}");
			return null;
		}
	}

	public static async Task<bool> WasUpdatedSinceAsync(Install install, DateTime sinceDate) {
		if (!install.IsSteamInstall) {
			Logger.Log(LogLevel.Debug, $"Install is not a Steam install, cannot check for updates");
			return false;
		}

		var lastUpdate = await GetLastUpdateDate(install.SteamAppId);
		if (lastUpdate == null) return false;

		return lastUpdate.Value > sinceDate;
	}
}