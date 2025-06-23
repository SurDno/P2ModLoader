using System.Runtime.InteropServices;
using P2ModLoader.Logging;

namespace P2ModLoader.WindowsFormsExtensions;

public class NoCaretTextBox : TextBox {
	[DllImport("user32.dll")]
	private static extern bool HideCaret(IntPtr hWnd);

	protected override void OnGotFocus(EventArgs e) {
		using var perf = PerformanceLogger.Log();
		base.OnGotFocus(e);
		HideCaret(Handle);
	}

	protected override void OnTextChanged(EventArgs e) {
		using var perf = PerformanceLogger.Log();
		base.OnTextChanged(e);
		HideCaret(Handle);
	}

	protected override void OnClick(EventArgs e) {
		using var perf = PerformanceLogger.Log();
		base.OnClick(e);
		HideCaret(Handle);
	}

	protected override void OnMouseClick(MouseEventArgs e) {
		using var perf = PerformanceLogger.Log();
		base.OnMouseClick(e);
		HideCaret(Handle);
	}
}