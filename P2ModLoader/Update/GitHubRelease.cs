using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace P2ModLoader.Update;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class GitHubRelease {
	[JsonPropertyName("tag_name")] public string TagName { get; set; } = string.Empty;
	[JsonPropertyName("assets")] public List<GitHubAsset> Assets { get; set; } = [];
	[JsonPropertyName("body")] public string Body { get; set; } = string.Empty;
}