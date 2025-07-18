using System.Diagnostics.CodeAnalysis;
using Mono.Cecil;
using P2ModLoader.Logging;

namespace P2ModLoader.Patching.Assembly.ILCloning;

public static class PropertyCloner {
	[SuppressMessage("ReSharper", "InvertIf")]
	public static PropertyDefinition CloneProperty(PropertyDefinition src, ModuleDefinition targetModule, TypeDefinition type) {
		using var perf = PerformanceLogger.Log();
		PropertyDefinition newProperty = new (src.Name, src.Attributes, targetModule.ImportReference(src.PropertyType));

		AttributesCloner.CloneAttributes(src, newProperty, targetModule);

		if (src.GetMethod != null) {
			var clonedGet = MethodCloner.CloneMethod(src.GetMethod, targetModule);
			type.Methods.Add(clonedGet);
			newProperty.GetMethod = clonedGet;
		}

		if (src.SetMethod != null) {
			var clonedSet = MethodCloner.CloneMethod(src.SetMethod, targetModule);
			type.Methods.Add(clonedSet);
			newProperty.SetMethod = clonedSet;
		}

		return newProperty;
	}
}