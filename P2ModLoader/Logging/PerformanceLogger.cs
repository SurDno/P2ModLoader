using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace P2ModLoader.Logging;

public sealed class PerformanceLogger : IDisposable {
	private readonly string? _context;
	private readonly Stopwatch? _stopwatch;

	private PerformanceLogger(string? context) {
		if (Logger.MinimumLevel < LogLevel.Performance) return;
		
		_context = context;
		_stopwatch = Stopwatch.StartNew();
        
		Logger.Log(LogLevel.Trace, $"[{_context}] Starting.");
	}

	public void Dispose() {
		if (_stopwatch == null) return;
		
		_stopwatch.Stop();
		var elapsed = _stopwatch.ElapsedMilliseconds;
        
		var perfLevel = elapsed switch {
			< 1 => LogLevel.Trace,
			< 1000 => LogLevel.Performance,
			< 5000 => LogLevel.Warning,
			_ => LogLevel.Error
		};
        
		Logger.Log(perfLevel, $"[{_context}] Completed in {elapsed}ms");
	}
    
	public static PerformanceLogger Log([CallerMemberName] string method = "", [CallerFilePath] string path = "") =>
		new ($"{Path.GetFileNameWithoutExtension(path)}.{method}");
}