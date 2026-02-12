using System.Diagnostics.CodeAnalysis;
using P2ModLoader.Helper;

namespace P2ModLoader.Forms;

public class ProgressForm : Form {
	private readonly ProgressBar _progressBar;
	private readonly Label _statusLabel;
	private readonly Label _titleLabel;

	public ProgressForm() { 	
		Icon = AppIcon.Instance;
		Width = 800;
		Height = 200;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		StartPosition = FormStartPosition.CenterScreen;
		Text = "Loading Mods";
		TopMost = true;
		
		_titleLabel = new Label {
			Text = "Patching Game Files...",
			AutoSize = true,
			Location = new Point(20, 10)
		};

		_progressBar = new ProgressBar {
			Width = Width - 80,
			Height = 23,
			Location = new Point(20, 40),
			Style = ProgressBarStyle.Continuous
		};

		_statusLabel = new Label {
			AutoSize = true,
			Location = new Point(20, 80)
		};

		Controls.AddRange([_titleLabel, _progressBar, _statusLabel]);
	}

	[AllowNull] 
	public sealed override string Text {
		get => base.Text;
		set => base.Text = value;
	}

	public string TitleText {
		get => _titleLabel.Text;
		set => _titleLabel.Text = value;
	}

	public void UpdateProgress(int current, int total, string status) { 	
		if (InvokeRequired) {
			Invoke(() => UpdateProgress(current, total, status));
			return;
		}

		_progressBar.Maximum = total;
		_progressBar.Value = Math.Min(current, total);
		_statusLabel.Text = status;
		Application.DoEvents(); 
	}

	public void UpdateProgress(string status) { 	
		if (InvokeRequired) {
			Invoke(() => UpdateProgress(_progressBar.Value, _progressBar.Maximum, status));
			return;
		}
		
		_statusLabel.Text = status;
		Application.DoEvents(); 
	}
}