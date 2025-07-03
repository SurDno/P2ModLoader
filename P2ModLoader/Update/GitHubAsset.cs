using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace P2ModLoader.Update;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class GitHubAsset {
	[JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = string.Empty;
}