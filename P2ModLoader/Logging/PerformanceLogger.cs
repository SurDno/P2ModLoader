using System.Diagnostics;
using System.Runtime.CompilerServices;
using P2ModLoader.Helper;

namespace P2ModLoader.Logging;

public sealed class PerformanceLogger : IDisposable {
	private static readonly PerformanceLogger DummyLogger = new(null);
	private readonly string? _context;
	private readonly Stopwatch? _stopwatch;

	private PerformanceLogger(string? context) {
		if (context == null) return;
		
		_context = context;
		_stopwatch = Stopwatch.StartNew();
        
		Logger.Log(LogLevel.Trace, $"[{_context}] Starting.");
	}

	public void Dispose() {
		if (_stopwatch == null) return;
		
		_stopwatch.Stop();
		var elapsed = _stopwatch.ElapsedMilliseconds;
        
		var perfLevel = elapsed switch {
			< 5 => LogLevel.Trace,
			< 1000 => LogLevel.Performance,
			< 5000 => LogLevel.Warning,
			_ => LogLevel.Error
		};
        
		Logger.Log(perfLevel, $"[{_context}] Completed in {elapsed}ms");
	}
    
    
	public static PerformanceLogger Log([CallerMemberName] string method = "", [CallerFilePath] string path = "") {
		return SettingsHolder.LogLevel < LogLevel.Performance ? DummyLogger
					: new($"{Path.GetFileNameWithoutExtension(path)}.{method}");
	}
}