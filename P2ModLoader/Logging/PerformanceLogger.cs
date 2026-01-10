using System.Diagnostics;

namespace P2ModLoader.Logging;

public sealed class PerformanceLogger : IDisposable {
	private readonly string _context;
	private readonly Stopwatch _stopwatch;

	private PerformanceLogger(string context) {
		_context = context;
		_stopwatch = Stopwatch.StartNew();
        
		Logger.Log(LogLevel.Trace, $"[{_context}] Starting.");
	}

	public void Dispose() {
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
	
	public static PerformanceLogger Log(string method, string type) => new($"{type}.{method}");
}