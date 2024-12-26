namespace P2ModLoader.Helper;

public static class ErrorHandler {
	
	public static void Handle(string msg, Exception? e, bool skipLogging = false) {
		var message = e != null ? ": " + e.Message : string.Empty;
		var stackTrace = e != null ? e.StackTrace + "\n\n" : string.Empty;
		
		if (e != null && !skipLogging)
			Logger.LogError(message + "\n" + stackTrace);
		
		MessageBox.Show($"{msg}{message}.\n\n{stackTrace}See P2ModLoader.log in your Logs directory for more info.",
			"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
	}
}