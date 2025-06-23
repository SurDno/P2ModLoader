using System.Runtime.CompilerServices;
using System.Text;
using P2ModLoader.Logging;

namespace P2ModLoader.Helper {
	[InterpolatedStringHandler]
	public readonly ref struct LogInterpolatedStringHandler {
		private readonly StringBuilder? _builder;

		public LogInterpolatedStringHandler(int literalLength, int formattedCount, LogLevel level, out bool shouldAppend) {
			if (level <= Logger.MinimumLevel && Logger.MinimumLevel != LogLevel.None) {
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