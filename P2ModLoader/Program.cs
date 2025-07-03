using System.Diagnostics;
using System.Security.Principal;
using P2ModLoader.Forms;
using P2ModLoader.Helper;
using P2ModLoader.Logging;

namespace P2ModLoader;

internal static class Program {
    [STAThread]
    private static void Main() {
        ApplicationConfiguration.Initialize();
        
        if (!IsRunningAsAdmin() && !CanWriteToAppDirectory()) {
            var result = MessageBox.Show(
                "P2ModLoader needs to write files to its installation directory but doesn't have sufficient " +
                "permissions. This commonly happens when the application is installed in Program Files or other " +
                "OS-protected directory.\n\n" +
                "The solution is either to move the application installation folder elsewhere or run it with elevated" +
                "permissions. Would you like to restart P2ModLoader as Administrator to continue?",
                "Administrator Privileges Required",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes) {
                try {
                    var currentExecutable = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(currentExecutable)) {
                        var startInfo = new ProcessStartInfo {
                            FileName = currentExecutable,
                            UseShellExecute = true,
                            Verb = "runas",
                            WorkingDirectory = Path.GetDirectoryName(currentExecutable)
                        };
                        Process.Start(startInfo);
                        Application.Exit();
                        return;
                    }
                } catch {
                    MessageBox.Show("Failed to restart as administrator. Please manually run the app as Administrator.",
                        "Restart Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        Logger.Log(LogLevel.Trace, $"Starting P2ModLoader...");
        SettingsSaver.LoadSettings();
        Application.Run(new MainForm());
    }

    private static bool IsRunningAsAdmin() {
        try {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        } catch {
            return false;
        }
    }

    private static bool CanWriteToAppDirectory() {
        return TestDirectoryWrite(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings")) && 
               TestDirectoryWrite(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs"));
    }

    private static bool TestDirectoryWrite(string directory) {
        try {
            Directory.CreateDirectory(directory);
            var testFile = Path.Combine(directory, $"writetest_{Guid.NewGuid()}.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        } catch {
            return false;
        }
    }
}