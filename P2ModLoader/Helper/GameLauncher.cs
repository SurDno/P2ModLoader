using System.Diagnostics;
using P2ModLoader.Patching;

namespace P2ModLoader.Helper {
	public static class GameLauncher {
		public static void LaunchExe() { 	
			var install = SettingsHolder.SelectedInstall;
			if (install == null) return;

			if (!SettingsHolder.IsPatched && !GamePatcher.TryPatch()) return;

			var gameExecutable = install.ExecutablePath;

			Process.Start(new ProcessStartInfo {
				FileName = gameExecutable,
				WorkingDirectory = Path.GetDirectoryName(gameExecutable)
			});
		}

		public static void LaunchSteam() { 	
			var install = SettingsHolder.SelectedInstall;
			if (install == null) return;

			if (!SettingsHolder.IsPatched && !GamePatcher.TryPatch()) return;

			var steamPath = InstallationLocator.FindSteam();
			if (steamPath == null || !install.IsSteamInstall) return;
			
			var steamProcess = new ProcessStartInfo {
				FileName = Path.Combine(steamPath, InstallationLocator.SteamExe),
				Arguments = $"-applaunch {install.SteamAppId}"
			};

			Process.Start(steamProcess);
		}
	}
}