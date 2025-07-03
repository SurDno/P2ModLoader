using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using P2ModLoader.Helper;

namespace P2ModLoader.Logging {
	[InterpolatedStringHandler]
	public readonly ref struct LogInterpolatedStringHandler {
		private readonly StringBuilder? _builder;

		public LogInterpolatedStringHandler(int literalLength, int formattedCount, LogLevel level, out bool shouldAppend) {
			if (level <= SettingsHolder.LogLevel && SettingsHolder.LogLevel != LogLevel.None) {
				_builder = new StringBuilder(literalLength);
				shouldAppend = true;
			} else {
				_builder = null;
				shouldAppend = false;
			}
		}

		public void AppendLiteral(string s) => _builder?.Append(s);
		public void AppendFormatted<T>(T value) => _builder?.Append(value);

		public void AppendFormatted<T>(T value, string? format) {
			if (value is IFormattable f)
				_builder?.Append(f.ToString(format, null));
			else
				_builder?.Append(value);
		}

		public override string ToString() => _builder?.ToString() ?? string.Empty;
	}
}