namespace P2ModLoader.Helper;

public static class FontHelper {
	private static readonly string[] MonospaceFonts = [
		"Consolas",
		"Courier New",
		"Lucida Console",
		"DejaVu Sans Mono",
		"Monaco",
		"Andale Mono"
	];

	public static Font GetMonospaceFont(float size = 9F) { 	
		foreach (var fontName in MonospaceFonts) {
			if (!IsFontInstalled(fontName)) continue;
			return new Font(fontName, size, FontStyle.Regular, GraphicsUnit.Point);
		}

		return new Font(FontFamily.GenericMonospace, size, FontStyle.Regular, GraphicsUnit.Point);
	}

	private static bool IsFontInstalled(string fontName) { 	
		using var testFont = new Font(fontName, 8.25f, FontStyle.Regular, GraphicsUnit.Point);
		return testFont.Name.Equals(fontName, StringComparison.InvariantCultureIgnoreCase);
	}
}