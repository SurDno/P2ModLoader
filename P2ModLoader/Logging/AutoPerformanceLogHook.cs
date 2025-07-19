using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using P2ModLoader.Helper;
using P2ModLoader.Logging;
using PostSharp.Aspects;
using PostSharp.Serialization;

[assembly: AutoPerformanceLogHook(AttributeTargetTypes = "P2ModLoader.*")]
[assembly: AutoPerformanceLogHook(AttributeTargetTypes = "P2ModLoader.Logging.*", AttributeExclude = true)]
// TODO: separate settings from the rest of helper methods and exclude just the settings.
[assembly: AutoPerformanceLogHook(AttributeTargetTypes = "P2ModLoader.Helper.*", AttributeExclude = true)]
namespace P2ModLoader.Logging;

[PSerializable]
[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
public class AutoPerformanceLogHook : OnMethodBoundaryAspect {

	public override void OnEntry(MethodExecutionArgs args) {
		if (SettingsHolder.LogLevel < LogLevel.Performance) return;
			
		var actualMethod = args.Method;
		var typeName = actualMethod.DeclaringType?.Name ?? string.Empty;
		var methodName = actualMethod.Name;
		
		var performanceLogger = PerformanceLogger.Log(methodName, typeName);
		args.MethodExecutionTag = performanceLogger;
	}

	public override void OnExit(MethodExecutionArgs args) {
		if (args.MethodExecutionTag is IDisposable disposable)
			disposable.Dispose();
	}

	private static bool IsPropertyAccessor(string methodName) => methodName.StartsWith("get_") || 
	                                                             methodName.StartsWith("set_");
	
	private static bool IsConstructor(string methodName) => methodName.StartsWith(".ctor") ||
	                                                        methodName.EndsWith(".cctor");
}