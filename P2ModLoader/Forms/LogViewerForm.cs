using P2ModLoader.Helper;
using P2ModLoader.Logging;

namespace P2ModLoader.Forms;

public sealed class LogViewerForm : Form {
	private readonly TextBox _logTextBox;

	public LogViewerForm() {
		Icon = AppIcon.Instance;
		Text = "P2ModLoader Logs";
		Size = new Size(1800, 600);

		_logTextBox = new TextBox {
			Dock = DockStyle.Fill,
			Multiline = true,
			ReadOnly = true,
			ScrollBars = ScrollBars.Vertical,
			Font = new Font("Consolas", 9),
			ContextMenuStrip = SetupContextMenu()
		};
       
		Controls.Add(_logTextBox);
		LoadLogs();
       
		Logger.LogMessageAdded += OnLogMessageAdded;
	}

	private ContextMenuStrip SetupContextMenu() {
		var contextMenu = new ContextMenuStrip();
		contextMenu.Items.Add("Copy", null, (_, _) => {
			if (!string.IsNullOrEmpty(_logTextBox.SelectedText))
				Clipboard.SetText(_logTextBox.SelectedText);
		});
		contextMenu.Items.Add("Select All", null, (_, _) => _logTextBox.SelectAll());
		return contextMenu;
	}

	private void LoadLogs() {
		_logTextBox.Text = string.Join(Environment.NewLine, Logger.GetAllMessages());
		UpdateLogPosition();
	}

	private void OnLogMessageAdded(string message) {
		if (InvokeRequired) {
			Invoke(() => OnLogMessageAdded(message));
			return;
		}
		_logTextBox.AppendText(Environment.NewLine + message);
		UpdateLogPosition();
	}

	private void UpdateLogPosition() {
		_logTextBox.SelectionStart = _logTextBox.Text.Length;
		_logTextBox.ScrollToCaret();
	}
    
	protected override void Dispose(bool disposing) {
		if (disposing) 
			Logger.LogMessageAdded -= OnLogMessageAdded;
		base.Dispose(disposing);
	}
}