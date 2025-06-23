using P2ModLoader.Forms;
using P2ModLoader.Helper;
using P2ModLoader.Logging;

namespace P2ModLoader;

internal static class Program {
	[STAThread]
	private static void Main() {
		Logger.Log(LogLevel.Trace, $"Starting P2ModLoader...");
		SettingsSaver.LoadSettings();

		ApplicationConfiguration.Initialize();
		Application.Run(new MainForm());
	}
}


