using System.Diagnostics;
using P2ModLoader.Patching;

namespace P2ModLoader.Helper {
	public static class GameLauncher {
		private const string EXE_PATH = "Pathologic.exe";

		public static void LaunchExe() {
			if (SettingsHolder.InstallPath == null)
				return;

			if (!SettingsHolder.IsPatched && !GamePatcher.TryPatch()) return;

			var gameExecutable = Path.Combine(SettingsHolder.InstallPath!, EXE_PATH);

			Process.Start(new ProcessStartInfo {
				FileName = gameExecutable,
				WorkingDirectory = Path.GetDirectoryName(gameExecutable)
			});
		}

		public static void LaunchSteam() {
			if (SettingsHolder.InstallPath == null)
				return;

			if (!SettingsHolder.IsPatched && !GamePatcher.TryPatch()) return;

			var steamProcess = new ProcessStartInfo {
				FileName = Path.Combine(InstallationLocator.FindSteam()!, InstallationLocator.STEAM_EXE),
				Arguments = "-applaunch 505230"
			};

			Process.Start(steamProcess);
		}
	}
}