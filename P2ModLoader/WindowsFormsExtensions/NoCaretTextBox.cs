using System.Runtime.InteropServices;

namespace P2ModLoader.WindowsFormsExtensions;

public class NoCaretTextBox : TextBox {
	[DllImport("user32.dll")]
	private static extern bool HideCaret(IntPtr hWnd);

	protected override void OnGotFocus(EventArgs e) { 	
		base.OnGotFocus(e);
		HideCaret(Handle);
	}

	protected override void OnTextChanged(EventArgs e) { 	
		base.OnTextChanged(e);
		HideCaret(Handle);
	}

	protected override void OnClick(EventArgs e) { 	
		base.OnClick(e);
		HideCaret(Handle);
	}

	protected override void OnMouseClick(MouseEventArgs e) { 	
		base.OnMouseClick(e);
		HideCaret(Handle);
	}
}