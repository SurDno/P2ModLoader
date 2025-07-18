using System.Text;
using System.Text.RegularExpressions;
using P2ModLoader.Logging;
using static P2ModLoader.Update.VersionComparison;

namespace P2ModLoader.Update;

public static partial class PatchnoteBuilder {
	public static string GetCumulativeReleaseNotes(List<GitHubRelease> releases) {
		using var perf = PerformanceLogger.Log();

		var notes = new StringBuilder();
		foreach (var release in releases.Where(r => IsLoaderNewer(r.TagName)).OrderBy(r => Version.Parse(r.TagName))) {
			var version = release.TagName.TrimStart('v');
			notes.AppendLine($"{version}:");
			var releaseBody = PatchnoteStartRegex().Replace(release.Body.Trim(), "- ");
			notes.AppendLine(releaseBody);
			notes.AppendLine();
		}

		return notes.ToString().TrimEnd();
	}

	[GeneratedRegex(@"(?m)^\* ")]
	private static partial Regex PatchnoteStartRegex();
}