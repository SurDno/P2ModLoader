namespace P2ModLoader.Forms;

public sealed class ProfileEditDialog : Form {
	private readonly TextBox _numberTextBox;

	private const byte BACKSPACE = 8;
	public string? UniqueName { get; private set; }

	public ProfileEditDialog(string prefix, string currentUniqueName) { 	
		Text = "Edit Profile Name";
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		StartPosition = FormStartPosition.CenterParent;
		Width = 300;
		Height = 190;
        
		var prefixLabel = new Label {
			Text = prefix,
			Width = 120, 
			Height = 25, 
			Location = new Point(0, 25),
			TextAlign = ContentAlignment.MiddleRight,
			AutoSize = false 
		};

		_numberTextBox = new TextBox {
			Text = currentUniqueName,
			Width = 130, 
			Location = new Point(prefixLabel.Right + 10, 23) 
		};
		_numberTextBox.KeyPress += (_, e) => {
			if (e.KeyChar == ' ' || (Path.GetInvalidFileNameChars().Contains(e.KeyChar) && e.KeyChar != BACKSPACE))
				e.Handled = true;
		};
		
		var okButton = new Button {
			Text = "OK",
			DialogResult = DialogResult.OK,
			Size = new Size(100, 36),
			Location = new Point(Width / 2 - 100 - 15, Height - 100)
		};

		var cancelButton = new Button {
			Text = "Cancel",
			DialogResult = DialogResult.Cancel,
			Size = new Size(100, 36),
			Location = new Point(Width / 2 + 15, Height - 100)
		};

		AcceptButton = okButton;
		CancelButton = cancelButton;

		Controls.AddRange([prefixLabel, _numberTextBox, okButton, cancelButton]);
	}

	protected override void OnFormClosing(FormClosingEventArgs e) { 	
		if (DialogResult == DialogResult.OK) 
			UniqueName = _numberTextBox.Text;
		
		base.OnFormClosing(e);
	}
}