using System.Reflection;
using P2ModLoader.Logging;

namespace P2ModLoader.Update;

public static class VersionComparison {
	public static readonly string CurrentLoaderVersion;

	static VersionComparison() {
		using var perf = PerformanceLogger.Log();
		var versionInfo = Assembly.GetExecutingAssembly().GetName().Version!;
		CurrentLoaderVersion = $"{versionInfo.Major}.{versionInfo.Minor}.{versionInfo.Build}";
	}
	
	public static bool IsLoaderNewer(string version) => Version.Parse(version) > Version.Parse(CurrentLoaderVersion);
	public static bool IsLoaderNewer(Version version) => version > Version.Parse(CurrentLoaderVersion);
}